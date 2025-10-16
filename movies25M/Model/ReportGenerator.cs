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
        private readonly object _lock = new object();
        private readonly string[] _targetGenres = { "Action", "Drama", "Comedy", "Fantasy" };

        public ReportGenerator(MovieLensData data)
        {
            _data = data;
        }

        // ---------- SINGLE-THREADED ----------
        public void GenerateAllReportsSingleThreaded(string outputFolder)
        {
            Directory.CreateDirectory(outputFolder);
            var bucket = BuildAggregates(_data.Ratings);
            WriteReports(bucket, outputFolder);
        }

        public void GenerateAllReportsMultiThreaded(string outputFolder)
        {
            Directory.CreateDirectory(outputFolder);

            int total = _data.Ratings.Count;
            const int chunkSize = 10000;

            var chunks = Enumerable.Range(0, (int)Math.Ceiling(total / (double)chunkSize))
                .Select(i =>
                {
                    int start = i * chunkSize;
                    int end = Math.Min(start + chunkSize, total);
                    return _data.Ratings.GetRange(start, end - start);
                })
                .ToList();

            var results = new List<AggregateBucket>();
            object mergeLock = new object();

            System.Threading.Tasks.Parallel.ForEach(
                chunks,
                new System.Threading.Tasks.ParallelOptions
                {
                    MaxDegreeOfParallelism = Environment.ProcessorCount  
                },
                chunk =>
                {
                    var local = BuildAggregates(chunk);
                    lock (mergeLock)
                    {
                        results.Add(local);
                    }
                });

            var merged = MergeBuckets(results);
            WriteReports(merged, outputFolder);
        }


        private AggregateBucket BuildAggregates(List<Rating> ratings)
        {
            var bucket = new AggregateBucket();
            foreach (var r in ratings)
            {
                if (!_data.Movies.TryGetValue(r.MovieId, out var movie)) continue;
                bucket.AddRating(r.MovieId, r.Value);
                foreach (var g in movie.Genres)
                    bucket.AddRatingForGenre(g, r.MovieId, r.Value);
            }
            return bucket;
        }

        private AggregateBucket MergeBuckets(List<AggregateBucket> list)
        {
            var merged = new AggregateBucket();
            foreach (var b in list) merged.Merge(b);
            return merged;
        }

        private void WriteReports(AggregateBucket stats, string outputFolder)
        {
            WriteTop10General(stats, outputFolder);
            foreach (var genre in _targetGenres)
                WriteTop10ByGenre(stats, outputFolder, genre);
        }

        private void WriteTop10General(AggregateBucket stats, string outputFolder)
        {
            var rows = stats.GetTopMoviesOverall(10)
                .Select(kv => new[] {
                    kv.Key.ToString(),
                    EscapeCsv(_data.Movies[kv.Key].Title),
                    kv.Value.Average.ToString("0.00"),
                    kv.Value.Count.ToString()
                });

            WriteCsv(Path.Combine(outputFolder, "Top10_General.csv"),
                     new[] { "MovieId", "Title", "AverageRating", "RatingCount" }, rows);
        }

        private void WriteTop10ByGenre(AggregateBucket stats, string outputFolder, string genre)
        {
            var rows = stats.GetTopMoviesByGenre(genre, 10)
                .Select(kv => new[] {
                    kv.Key.ToString(),
                    EscapeCsv(_data.Movies[kv.Key].Title),
                    genre,
                    kv.Value.Average.ToString("0.00"),
                    kv.Value.Count.ToString()
                });

            WriteCsv(Path.Combine(outputFolder, $"Top10_Genre_{genre}.csv"),
                     new[] { "MovieId", "Title", "Genre", "AverageRating", "RatingCount" }, rows);
        }

        private void WriteCsv(string path, string[] header, IEnumerable<string[]> rows)
        {
            using var sw = new StreamWriter(path, false);
            sw.WriteLine(string.Join(",", header));
            foreach (var r in rows) sw.WriteLine(string.Join(",", r));
        }

        private string EscapeCsv(string s)
        {
            if (s.Contains(",") || s.Contains("\""))
                return $"\"{s.Replace("\"", "\"\"")}\"";
            return s;
        }
    }

    public class SumCount
    {
        public double Sum { get; set; }
        public int Count { get; set; }
        public double Average => Count == 0 ? 0 : Sum / Count;
        public void Add(double r) { Sum += r; Count++; }
        public void Merge(SumCount other) { Sum += other.Sum; Count += other.Count; }
    }

    public class AggregateBucket
    {
        private readonly Dictionary<int, SumCount> overall = new();
        private readonly Dictionary<string, Dictionary<int, SumCount>> byGenre =
            new(StringComparer.OrdinalIgnoreCase);

        public void AddRating(int movieId, double rating)
        {
            if (!overall.TryGetValue(movieId, out var sc)) overall[movieId] = sc = new SumCount();
            sc.Add(rating);
        }

        public void AddRatingForGenre(string genre, int movieId, double rating)
        {
            if (string.IsNullOrEmpty(genre)) return;
            if (!byGenre.TryGetValue(genre, out var dict)) byGenre[genre] = dict = new();
            if (!dict.TryGetValue(movieId, out var sc)) dict[movieId] = sc = new SumCount();
            sc.Add(rating);
        }

        public void Merge(AggregateBucket other)
        {
            foreach (var kv in other.overall)
            {
                if (!overall.TryGetValue(kv.Key, out var sc)) overall[kv.Key] = sc = new SumCount();
                sc.Merge(kv.Value);
            }

            foreach (var g in other.byGenre)
            {
                if (!byGenre.TryGetValue(g.Key, out var dict)) byGenre[g.Key] = dict = new();
                foreach (var kv in g.Value)
                {
                    if (!dict.TryGetValue(kv.Key, out var sc)) dict[kv.Key] = sc = new SumCount();
                    sc.Merge(kv.Value);
                }
            }
        }

        public IEnumerable<KeyValuePair<int, SumCount>> GetTopMoviesOverall(int n) =>
            overall.OrderByDescending(kv => kv.Value.Average)
                   .ThenByDescending(kv => kv.Value.Count)
                   .Take(n);

        public IEnumerable<KeyValuePair<int, SumCount>> GetTopMoviesByGenre(string genre, int n)
        {
            if (!byGenre.TryGetValue(genre, out var dict)) return Enumerable.Empty<KeyValuePair<int, SumCount>>();
            return dict.OrderByDescending(kv => kv.Value.Average)
                       .ThenByDescending(kv => kv.Value.Count)
                       .Take(n);
        }
    }
}
