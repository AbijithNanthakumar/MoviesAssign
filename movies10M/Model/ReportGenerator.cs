using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;

namespace Model
{
    public class ReportGenerator
    {
        private readonly MovieLensData _data;
        private readonly object _mergeLock = new object();

        // list of target genres (case-insensitive)
        private readonly string[] _targetGenres = new[] { "Action", "Drama", "Comedy", "Fantasy" };

        public ReportGenerator(MovieLensData data)
        {
            _data = data;
        }

        // SINGLE-THREADED: build aggregates over all ratings then write reports
        public void GenerateAllReportsSingleThreaded(string outputFolder)
        {
            Directory.CreateDirectory(outputFolder);

            var bucket = BuildAggregates(_data.Ratings);

            WriteTop10General(bucket, outputFolder);
            foreach (var genre in _targetGenres)
                WriteTop10ByGenre(bucket, outputFolder, genre);
        }

        // MULTI-THREADED: split Ratings into chunks of 10,000 and spawn threads
        public void GenerateAllReportsMultiThreaded(string outputFolder)
        {
            Directory.CreateDirectory(outputFolder);

            int total = _data.Ratings.Count;
            const int chunkSize = 10000;
            int numThreads = (int)Math.Ceiling(total / (double)chunkSize);
            if (numThreads < 1) numThreads = 1;

            var buckets = new AggregateBucket[numThreads];
            var threads = new Thread[numThreads];

            for (int i = 0; i < numThreads; i++)
            {
                int idx = i;
                threads[i] = new Thread(() =>
                {
                    int start = idx * chunkSize;
                    int end = Math.Min(start + chunkSize, total);
                    var chunk = _data.Ratings.GetRange(start, Math.Max(0, end - start));
                    var local = BuildAggregates(chunk);
                    lock (_mergeLock)
                    {
                        buckets[idx] = local;
                    }
                })
                {
                    Name = $"T{idx + 1}",
                    IsBackground = false // foreground
                };
                threads[i].Start();
            }

            // wait
            foreach (var t in threads) t.Join();

            // merge
            var merged = MergeBuckets(buckets.Where(b => b != null).ToList());

            WriteTop10General(merged, outputFolder);
            foreach (var genre in _targetGenres)
                WriteTop10ByGenre(merged, outputFolder, genre);
        }

        #region Aggregation
        private AggregateBucket BuildAggregates(List<Rating> ratings)
        {
            var bucket = new AggregateBucket();
            foreach (var r in ratings)
            {
                if (!_data.Movies.TryGetValue(r.MovieId, out var movie)) continue;
                bucket.AddRating(r.MovieId, r.Value);

                foreach (var g in movie.Genres)
                {
                    bucket.AddRatingForGenre(g, r.MovieId, r.Value);
                }
            }
            return bucket;
        }

        private AggregateBucket MergeBuckets(List<AggregateBucket> buckets)
        {
            var merged = new AggregateBucket();
            foreach (var b in buckets) merged.Merge(b);
            return merged;
        }
        #endregion

        #region Writers
        private void WriteTop10General(AggregateBucket stats, string outputFolder)
        {
            var rows = stats.GetTopMoviesOverall(10)
                .Select(kv => new { Id = kv.Key, Avg = kv.Value.Average, Count = kv.Value.Count })
                .Select(x => new string[] { x.Id.ToString(), EscapeCsv(_data.Movies[x.Id].Title), x.Avg.ToString("0.00"), x.Count.ToString() });

            string path = Path.Combine(outputFolder, "Top10_General.csv");
            WriteCsv(path, new[] { "MovieId", "Title", "AverageRating", "RatingCount" }, rows);
        }

        private void WriteTop10ByGenre(AggregateBucket stats, string outputFolder, string genre)
        {
            var rows = stats.GetTopMoviesByGenre(genre, 10)
                .Select(kv => new { Id = kv.Key, Avg = kv.Value.Average, Count = kv.Value.Count })
                .Select(x => new string[] { x.Id.ToString(), EscapeCsv(_data.Movies[x.Id].Title), genre, x.Avg.ToString("0.00"), x.Count.ToString() });

            string safe = genre.Replace(" ", "_");
            string path = Path.Combine(outputFolder, $"Top10_Genre_{safe}.csv");
            WriteCsv(path, new[] { "MovieId", "Title", "Genre", "AverageRating", "RatingCount" }, rows);
        }

        private void WriteCsv(string path, string[] header, IEnumerable<string[]> rows)
        {
            using (var sw = new StreamWriter(path, false))
            {
                sw.WriteLine(string.Join(",", header));
                foreach (var r in rows)
                {
                    sw.WriteLine(string.Join(",", r));
                }
            }
        }

        private string EscapeCsv(string s)
        {
            if (s == null) return "";
            if (s.Contains(",") || s.Contains("\"") || s.Contains("\n"))
                return $"\"{s.Replace("\"", "\"\"")}\"";
            return s;
        }
        #endregion
    }

    #region Aggregation helper types
    public class SumCount
    {
        public double Sum { get; set; } = 0.0;
        public int Count { get; set; } = 0;
        public double Average => Count == 0 ? 0.0 : Sum / Count;
        public void Add(double v) { Sum += v; Count++; }
        public void Merge(SumCount other) { Sum += other.Sum; Count += other.Count; }
    }

    public class AggregateBucket
    {
        private Dictionary<int, SumCount> overall = new Dictionary<int, SumCount>();
        private Dictionary<string, Dictionary<int, SumCount>> byGenre = new Dictionary<string, Dictionary<int, SumCount>>(StringComparer.OrdinalIgnoreCase);

        public void AddRating(int movieId, double rating)
        {
            if (!overall.TryGetValue(movieId, out var sc)) { sc = new SumCount(); overall[movieId] = sc; }
            sc.Add(rating);
        }

        public void AddRatingForGenre(string genre, int movieId, double rating)
        {
            if (string.IsNullOrEmpty(genre)) return;
            if (!byGenre.TryGetValue(genre, out var dict)) { dict = new Dictionary<int, SumCount>(); byGenre[genre] = dict; }
            if (!dict.TryGetValue(movieId, out var sc)) { sc = new SumCount(); dict[movieId] = sc; }
            sc.Add(rating);
        }

        public void Merge(AggregateBucket other)
        {
            foreach (var kv in other.overall)
            {
                if (!overall.TryGetValue(kv.Key, out var sc)) { sc = new SumCount(); overall[kv.Key] = sc; }
                overall[kv.Key].Merge(kv.Value);
            }

            foreach (var gen in other.byGenre)
            {
                if (!byGenre.TryGetValue(gen.Key, out var dict)) { dict = new Dictionary<int, SumCount>(); byGenre[gen.Key] = dict; }
                foreach (var kv in gen.Value)
                {
                    if (!byGenre[gen.Key].TryGetValue(kv.Key, out var sc)) { sc = new SumCount(); byGenre[gen.Key][kv.Key] = sc; }
                    byGenre[gen.Key][kv.Key].Merge(kv.Value);
                }
            }
        }

        public IEnumerable<KeyValuePair<int, SumCount>> GetTopMoviesOverall(int n)
        {
            return overall
                .Where(kv => kv.Value.Count > 0)
                .OrderByDescending(kv => kv.Value.Average)
                .ThenByDescending(kv => kv.Value.Count)
                .Take(n);
        }

        public IEnumerable<KeyValuePair<int, SumCount>> GetTopMoviesByGenre(string genre, int n)
        {
            if (!byGenre.TryGetValue(genre, out var dict)) return Enumerable.Empty<KeyValuePair<int, SumCount>>();
            return dict
                .Where(kv => kv.Value.Count > 0)
                .OrderByDescending(kv => kv.Value.Average)
                .ThenByDescending(kv => kv.Value.Count)
                .Take(n);
        }
    }
    #endregion
}
