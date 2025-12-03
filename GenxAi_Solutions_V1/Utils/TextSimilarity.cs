namespace GenxAi_Solutions_V1.Utils
{
    public static class TextSimilarity
    {
        public static string Normalize(string x) =>
            (x ?? "").Replace("_", " ").Trim().ToLowerInvariant();

        public static float[] Average(params float[][] vectors)
        {
            if (vectors == null || vectors.Length == 0) return Array.Empty<float>();
            int n = vectors[0].Length;
            var sum = new float[n];
            int count = 0;

            foreach (var v in vectors)
            {
                if (v == null || v.Length != n) continue;
                for (int i = 0; i < n; i++) sum[i] += v[i];
                count++;
            }
            if (count == 0) return Array.Empty<float>();
            for (int i = 0; i < n; i++) sum[i] /= count;
            return sum;
        }

        public static double Cosine(float[] a, float[] b)
        {
            if (a == null || b == null || a.Length != b.Length || a.Length == 0) return 0.0;
            double dot = 0, na = 0, nb = 0;
            for (int i = 0; i < a.Length; i++)
            {
                dot += a[i] * b[i];
                na += a[i] * a[i];
                nb += b[i] * b[i];
            }
            if (na == 0 || nb == 0) return 0.0;
            return dot / (Math.Sqrt(na) * Math.Sqrt(nb));
        }

        // Jaro-Winkler (typo tolerant)
        public static double JaroWinkler(string s1, string s2)
        {
            s1 ??= ""; s2 ??= "";
            if (s1 == s2) return 1.0;
            int matchDistance = Math.Max(s1.Length, s2.Length) / 2 - 1;
            bool[] s1Matches = new bool[s1.Length];
            bool[] s2Matches = new bool[s2.Length];

            int matches = 0;
            for (int i = 0; i < s1.Length; i++)
            {
                int start = Math.Max(0, i - matchDistance);
                int end = Math.Min(i + matchDistance + 1, s2.Length);
                for (int j = start; j < end; j++)
                {
                    if (s2Matches[j] || s1[i] != s2[j]) continue;
                    s1Matches[i] = s2Matches[j] = true;
                    matches++;
                    break;
                }
            }
            if (matches == 0) return 0.0;

            double t = 0;
            int k = 0;
            for (int i = 0; i < s1.Length; i++)
            {
                if (!s1Matches[i]) continue;
                while (!s2Matches[k]) k++;
                if (s1[i] != s2[k]) t++;
                k++;
            }
            t /= 2.0;

            double m = matches;
            double jaro = (m / s1.Length + m / s2.Length + (m - t) / m) / 3.0;

            int l = 0;
            for (; l < Math.Min(4, Math.Min(s1.Length, s2.Length)); l++)
                if (s1[l] != s2[l]) break;

            return jaro + l * 0.1 * (1 - jaro);
        }
    }
}

