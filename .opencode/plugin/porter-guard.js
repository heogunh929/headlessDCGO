// porter-guard: enforce the porting pipeline's blast-radius + context guarantees.
//
// Two jobs (see porting/ design):
//   1. chat.params  — for the local `porter` agent on the ollama provider, force
//      num_ctx >= 32768 (ollama defaults to 4096, which silently truncates the
//      primitive catalog and breaks the STOP rule) and pin a low temperature.
//   2. tool.execute.before — while the `porter` agent is driving, allow writes
//      ONLY inside the whitelist (card mirror folders, the STOP-aggregation dir,
//      and build artefacts). porter writes NO tests — the gate is the strong-model-
//      owned CardEffect.Binding.Auto test. Everything else — engine core, other
//      cards, tests/, and especially the read-only DCGO/ originals — is denied.
//      Also blocks commits/pushes (the user commits) from any agent.
//
// The guard is scoped to the porter agent by tracking sessionID -> agent from
// chat.params (tool.execute.before does not carry the agent name itself).

const MIN_NUM_CTX = 32768;

// Write whitelist, matched against the project-root-relative POSIX path.
// NOTE: porter writes NO tests — tests/ is intentionally excluded. The gate is
// the strong-model-owned CardEffect.Binding.Auto test.
const WRITE_ALLOW = [
  /^src\/HeadlessDCGO\.Engine\/Assets\/Scripts\/CardEffect\/[^/]+\/[^/]+\//, // card mirrors <SET>/<COLOR>/
  /^porting\/stop\//,                                                         // STOP aggregation
  /(^|\/)bin\//,                                                              // build artefacts
  /(^|\/)obj\//,
];

// Hard denials that apply to EVERY agent (not just porter).
const DCGO_RE = /(^|\/)DCGO\//;
const COMMIT_RE = /\bgit\s+(commit|push)\b/;

function toRel(directory, p) {
  if (!p) return null;
  let s = String(p).replace(/\\/g, "/");
  const root = String(directory || "").replace(/\\/g, "/").replace(/\/+$/, "");
  if (root && s.startsWith(root + "/")) s = s.slice(root.length + 1);
  else if (root && s === root) s = "";
  s = s.replace(/^\.\//, "");
  return s;
}

export const PorterGuard = async ({ directory }) => {
  const agentBySession = new Map();

  return {
    // 1. num_ctx + temperature for the local porter model.
    "chat.params": async (input, output) => {
      if (input.agent) agentBySession.set(input.sessionID, input.agent);
      const providerID = input.provider?.info?.id || input.model?.providerID || "";
      if (input.agent === "porter" && /ollama/i.test(String(providerID))) {
        output.options = output.options || {};
        if (!(output.options.num_ctx >= MIN_NUM_CTX)) output.options.num_ctx = MIN_NUM_CTX;
        output.temperature = 0.1;
      }
    },

    // Keep the session->agent map fresh even when chat.params is skipped.
    "chat.message": async (input) => {
      if (input.agent) agentBySession.set(input.sessionID, input.agent);
    },

    // 2. Write-path whitelist + universal commit/DCGO denials.
    "tool.execute.before": async (input, output) => {
      const args = output?.args || {};
      const agent = agentBySession.get(input.sessionID);

      // Universal: never let an automated agent commit/push or mutate DCGO/.
      if (input.tool === "bash") {
        const cmd = String(args.command || "");
        if (COMMIT_RE.test(cmd)) {
          throw new Error("porter-guard: git commit/push is forbidden — the user commits.");
        }
        if (agent === "porter" && DCGO_RE.test(cmd) && /(>|>>|\bsed\s+-i\b|\btee\b|\bcp\b|\bmv\b|\brm\b)/.test(cmd)) {
          throw new Error("porter-guard: DCGO/ is read-only.");
        }
        return; // other bash commands (build, run-tests, discovery) pass
      }

      if (input.tool !== "write" && input.tool !== "edit" && input.tool !== "patch") return;
      if (agent !== "porter") return; // only the porter is sandboxed to the whitelist

      const rel = toRel(directory, args.filePath || args.path);
      if (rel === null || rel === "") return;

      if (DCGO_RE.test(rel)) {
        throw new Error(`porter-guard: DCGO/ is read-only — refused write to ${rel}.`);
      }
      if (!WRITE_ALLOW.some((re) => re.test(rel))) {
        throw new Error(
          `porter-guard: write to '${rel}' is outside the porting whitelist ` +
            `(card mirror <SET>/<COLOR>/, porting/stop/). porter writes NO tests. ` +
            `If a needed change falls outside, STOP and record it for 강모델.`
        );
      }
    },
  };
};
