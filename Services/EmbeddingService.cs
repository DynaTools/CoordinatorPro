using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;

namespace CoordinatorPro.Services
{
    public static class EmbeddingService
    {
        private static InferenceSession _session;
        private static readonly object _lock = new object();

        private static readonly Dictionary<string, string> AbbreviationExpansion = new Dictionary<string, string>
        {
            {"ext", "exterior"}, {"int", "interior"}, {"apt", "apartment"},
            {"rm", "room"}, {"br", "bedroom"}, {"lvl", "level"},
            {"fl", "floor"}, {"clg", "ceiling"}, {"pkg", "parking"},
            {"mech", "mechanical"}, {"elec", "electrical"}, {"struct", "structural"},
            {"vert", "vertical"}, {"horiz", "horizontal"}, {"temp", "temporary"},
            {"perm", "permanent"}, {"res", "residential"}, {"comm", "commercial"}
        };

        public static bool Initialize()
        {
            if (_session != null) return true;

            lock (_lock)
            {
                if (_session != null) return true;

                try
                {
                    string assemblyPath = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
                    string modelPath = Path.Combine(assemblyPath, "model.onnx");

                    if (!File.Exists(modelPath))
                    {
                        System.Diagnostics.Debug.WriteLine($"Modelo ONNX não encontrado: {modelPath}");
                        return false;
                    }

                    _session = new InferenceSession(modelPath);
                    System.Diagnostics.Debug.WriteLine("Modelo ONNX carregado com sucesso");
                    return true;
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Erro ao carregar modelo ONNX: {ex.Message}");
                    return false;
                }
            }
        }

        public static string PreprocessText(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
                return "";

            text = text.ToLowerInvariant().Trim();

            foreach (var abbr in AbbreviationExpansion)
            {
                text = System.Text.RegularExpressions.Regex.Replace(
                    text,
                    $@"\b{abbr.Key}\b",
                    abbr.Value,
                    System.Text.RegularExpressions.RegexOptions.IgnoreCase
                );
            }

            text = System.Text.RegularExpressions.Regex.Replace(text, @"[^\w\s]", " ");
            text = System.Text.RegularExpressions.Regex.Replace(text, @"\s+", " ").Trim();

            return text;
        }

        public static float[] GenerateEmbedding(string text)
        {
            if (_session == null)
            {
                throw new InvalidOperationException("Modelo ONNX não inicializado");
            }

            try
            {
                text = PreprocessText(text);

                var tokens = SimpleTokenize(text);
                var inputIds = tokens.Select(t => (long)t).ToArray();

                var inputTensor = new DenseTensor<long>(inputIds, new[] { 1, inputIds.Length });

                var inputs = new List<NamedOnnxValue>
                {
                    NamedOnnxValue.CreateFromTensor("input_ids", inputTensor)
                };

                using (var results = _session.Run(inputs))
                {
                    var outputTensor = results.First().AsEnumerable<float>().ToArray();
                    return outputTensor;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Erro ao gerar embedding: {ex.Message}");
                return new float[384];
            }
        }

        private static int[] SimpleTokenize(string text)
        {
            var tokens = new List<int> { 101 };

            foreach (char c in text)
            {
                if (char.IsLetterOrDigit(c))
                    tokens.Add((int)c);
                else if (char.IsWhiteSpace(c))
                    tokens.Add(0);
            }

            tokens.Add(102);

            const int maxLength = 128;
            if (tokens.Count > maxLength)
                tokens = tokens.Take(maxLength).ToList();
            else
                while (tokens.Count < maxLength)
                    tokens.Add(0);

            return tokens.ToArray();
        }

        public static float CosineSimilarity(float[] a, float[] b)
        {
            if (a.Length != b.Length)
                return 0f;

            float dotProduct = 0f;
            float normA = 0f;
            float normB = 0f;

            for (int i = 0; i < a.Length; i++)
            {
                dotProduct += a[i] * b[i];
                normA += a[i] * a[i];
                normB += b[i] * b[i];
            }

            if (normA == 0f || normB == 0f)
                return 0f;

            return dotProduct / (float)(Math.Sqrt(normA) * Math.Sqrt(normB));
        }

        public static void Dispose()
        {
            _session?.Dispose();
            _session = null;
        }
    }
}