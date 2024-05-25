using Newtonsoft.Json;
using System.Collections.Generic;

namespace RoleplayingSpeechDalamud.GPT_API
{
    public class LogitBias
    {
        [JsonProperty("187")]
        public float _187 { get; set; }
    }
    internal class GPTRequest
    {
        public GPTRequest(string name, string prompt, GPTHistory history,
     string character, string instruction_template, string preset = "None")
        {
        }
        public GPTRequest(string name, string prompt, string preset = "None")
        {
            this.engine_id = "cassandra-lit-6-9b";
            this.prompt = prompt;
            this.max_tokens = 70;
            this.do_sample = true;
            this.temperature = 0.7f;
            this.top_p = 0.9f;
            this.typical_p = 0.9f;
            this.epsilon_cutoff = 0;
            this.eta_cutoff = 0;
            this.tfs = 1;
            this.top_a = 0;
            this.repetition_penalty = 1.17f;
            this.repetition_penalty_range = 0;
            this.top_k = 0;
            this.min_length = 0;
            this.no_repeat_ngram_size = 0;
            this.num_beams = 1;
            this.penalty_alpha = 0;
            this.length_penalty = 1;
            this.early_stopping = false;
            this.mirostat_mode = 0;
            this.mirostat_tau = 5;
            this.mirostat_eta = 0.1f;
            this.seed = -1;
            this.add_bos_token = false;
            this.truncation_length = 2048;
            this.ban_eos_token = false;
            this.skip_special_tokens = true;
            this.stream = false;
            this.logprobs = 10;
            this.logit_bias = new LogitBias() { _187 = -0.5f };
        }

        private string engine_id;

        public string prompt { get; set; }
        public int max_tokens { get; set; }
        public bool do_sample { get; set; }
        public float temperature { get; set; }
        public float top_p { get; set; }
        public float typical_p { get; set; }
        public int epsilon_cutoff { get; set; }
        public int eta_cutoff { get; set; }
        public int tfs { get; set; }
        public int top_a { get; set; }
        public float repetition_penalty { get; set; }
        public int repetition_penalty_range { get; set; }
        public int top_k { get; set; }
        public int min_length { get; set; }
        public int no_repeat_ngram_size { get; set; }
        public int num_beams { get; set; }
        public int penalty_alpha { get; set; }
        public int length_penalty { get; set; }
        public bool early_stopping { get; set; }
        public int mirostat_mode { get; set; }
        public int mirostat_tau { get; set; }
        public float mirostat_eta { get; set; }
        public int seed { get; set; }
        public bool add_bos_token { get; set; }
        public int truncation_length { get; set; }
        public bool ban_eos_token { get; set; }
        public bool skip_special_tokens { get; set; }
        public LogitBias logit_bias { get; set; }
        private bool stream;

        public int logprobs { get; private set; }
        public List<object> stopping_strings { get; set; }

    }
}
