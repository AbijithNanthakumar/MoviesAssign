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

        public ReportGenerator(MovieLensData data)
        {
            _data = data;
        }

        public void GenerateAllReportsSingleThreaded(string outputFolder)
        {
            Directory.CreateDirectory(outputFolder);

            var stats = BuildAggregates(_data.Ratings);

            WriteTop10General(stats, outputFolder);
            WriteTop10ByGender(stats, outputFolder, "M");
            WriteTop10ByGender(stats, outputFolder, "F");

            WriteTop10ByGenre(stats, outputFolder, "Action");
            WriteTop10ByGenre(stats, outputFolder, "Drama");
            WriteTop10ByGenre(stats, outputFolder, "Comedy");
            WriteTop10ByGenre(stats, outputFolder, "Fantasy");

            WriteTop10ByAgeGroups(stats, outputFolder, "<18", u => u.Age < 18);
            WriteTop10ByAgeGroups(stats, outputFolder, "18-29", u => u.Age >= 18 && u.Age < 30);
            WriteTop10ByAgeGroups(stats, outputFolder, "30+", u => u.Age >= 30);
        }

        public void GenerateAllReportsMultiThreaded(string outputFolder)
        {
            Directory.CreateDirectory(outputFolder);

            var totalRatings = _data.Ratings.Count;
            const int chunkSize = 10000;
            int numThreads = (int)Math.Ceiling(totalRatings / (double)chunkSize);
            if (numThreads < 1) numThreads = 1;

            var threadResults = new List<AggregateBucket>(numThreads);
            var threads = new Thread[numThreads];

            for (int i = 0; i < numThreads; i++)
            {
                int chunkIndex = i;
                threadResults.Add(new AggregateBucket());
                threads[i] = new Thread(() =>
                {
                    int start = chunkIndex * chunkSize;
                    int end = Math.Min(start + chunkSize, totalRatings);
                    var ratingsChunk = _data.Ratings.GetRange(start, Math.Max(0, end - start));
                    var localAgg = BuildAggregates(ratingsChunk);
                    lock (_mergeLock)
                    {
                        threadResults[chunkIndex] = localAgg;
                    }
                })
                {
                    Name = $"T{chunkIndex + 1}",
                    IsBackground = false 
                };
                threads[i].Start();
            }

            foreach (var t in threads) t.Join();

            var merged = MergeBuckets(threadResults);

            WriteTop10General(merged, outputFolder);
            WriteTop10ByGender(merged, outputFolder, "M");
            WriteTop10ByGender(merged, outputFolder, "F");

            WriteTop10ByGenre(merged, outputFolder, "Action");
            WriteTop10ByGenre(merged, outputFolder, "Drama");
            WriteTop10ByGenre(merged, outputFolder, "Comedy");
            WriteTop10ByGenre(merged, outputFolder, "Fantasy");

            WriteTop10ByAgeGroups(merged, outputFolder, "<18", u => u.Age < 18);
            WriteTop10ByAgeGroups(merged, outputFolder, "18-29", u => u.Age >= 18 && u.Age < 30);
            WriteTop10ByAgeGroups(merged, outputFolder, "30+", u => u.Age >= 30);
        }

        private AggregateBucket BuildAggregates(List<Rating> ratings)
        {
            var bucket = new AggregateBucket();

            foreach (var r in ratings)
            {
                if (!_data.Movies.ContainsKey(r.MovieId)) continue;

                
                bucket.AddRating(r.MovieId, r.Value);

                if (_data.Users.TryGetValue(r.UserId, out var user))
                {
                    bucket.AddRatingForGender(r.MovieId, user.Gender, r.Value);

                    if (user.Age < 18) bucket.AddRatingForAgeGroup(r.MovieId, AgeGroup.Under18, r.Value);
                    else if (user.Age >= 18 && user.Age < 30) bucket.AddRatingForAgeGroup(r.MovieId, AgeGroup.Age18To29, r.Value);
                    else bucket.AddRatingForAgeGroup(r.MovieId, AgeGroup.Age30Plus, r.Value);
                }

                var movie = _data.Movies[r.MovieId];
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
            foreach (var b in buckets)
            {
                merged.Merge(b);
            }
            return merged;
        }

        #region Report Writers (CSV)
        private void WriteTop10General(AggregateBucket stats, string outputFolder)
        {
            var rows = stats.GetTopMoviesOverall(10)
                .Select(x => new { MovieId = x.Key, Title = _data.Movies[x.Key].Title, Avg = x.Value.Average, Count = x.Value.Count });

            string file = Path.Combine(outputFolder, "Top10_General.csv");
            WriteCsv(file, new[] { "MovieId", "Title", "AverageRating", "RatingCount" },
                rows.Select(r => new string[] { r.MovieId.ToString(), EscapeCsv(r.Title), r.Avg.ToString("0.00"), r.Count.ToString() }));
        }

        private void WriteTop10ByGender(AggregateBucket stats, string outputFolder, string gender)
        {
            var rows = stats.GetTopMoviesByGender(gender, 10)
                .Select(x => new { MovieId = x.Key, Title = _data.Movies[x.Key].Title, Avg = x.Value.Average, Count = x.Value.Count });

            string suffix = gender == "M" ? "Male" : (gender == "F" ? "Female" : gender);
            string file = Path.Combine(outputFolder, $"Top10_{suffix}.csv");
            WriteCsv(file, new[] { "MovieId", "Title", "AverageRating", "RatingCount" },
                rows.Select(r => new string[] { r.MovieId.ToString(), EscapeCsv(r.Title), r.Avg.ToString("0.00"), r.Count.ToString() }));
        }

        private void WriteTop10ByGenre(AggregateBucket stats, string outputFolder, string genre)
        {
            var rows = stats.GetTopMoviesByGenre(genre, 10)
                .Select(x => new { MovieId = x.Key, Title = _data.Movies[x.Key].Title, Avg = x.Value.Average, Count = x.Value.Count });

            string safeGenre = genre.Replace(" ", "_");
            string file = Path.Combine(outputFolder, $"Top10_Genre_{safeGenre}.csv");
            WriteCsv(file, new[] { "MovieId", "Title", "Genre", "AverageRating", "RatingCount" },
                rows.Select(r => new string[] { r.MovieId.ToString(), EscapeCsv(r.Title), genre, r.Avg.ToString("0.00"), r.Count.ToString() }));
        }

        private void WriteTop10ByAgeGroups(AggregateBucket stats, string outputFolder, string label, Func<User, bool> predicate)
        {
            AgeGroup ag = label == "<18" ? AgeGroup.Under18 : label == "18-29" ? AgeGroup.Age18To29 : AgeGroup.Age30Plus;
            var rows = stats.GetTopMoviesByAgeGroup(ag, 10)
                .Select(x => new { MovieId = x.Key, Title = _data.Movies[x.Key].Title, Avg = x.Value.Average, Count = x.Value.Count });

            string safeLabel = label.Replace(" ", "_").Replace("+", "plus").Replace("<", "lt").Replace("-", "_");
            string file = Path.Combine(outputFolder, $"Top10_Age_{safeLabel}.csv");
            WriteCsv(file, new[] { "MovieId", "Title", "AgeGroup", "AverageRating", "RatingCount" },
                rows.Select(r => new string[] { r.MovieId.ToString(), EscapeCsv(r.Title), label, r.Avg.ToString("0.00"), r.Count.ToString() }));
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

        private string EscapeCsv(string input)
        {
            if (input == null) return "";
            if (input.Contains(",") || input.Contains("\"") || input.Contains("\n"))
            {
                return $"\"{input.Replace("\"", "\"\"")}\"";
            }
            return input;
        }
        #endregion
    }

    #region Helper Aggregate Bucket classes
    public class SumCount
    {
        public long Sum { get; set; } = 0;
        public int Count { get; set; } = 0;
        public double Average => Count == 0 ? 0 : (double)Sum / Count;
        public void Add(int rating) { Sum += rating; Count++; }
        public void Merge(SumCount other) { Sum += other.Sum; Count += other.Count; }
    }

    public enum AgeGroup { Under18, Age18To29, Age30Plus }

    public class AggregateBucket
    {
        private Dictionary<int, SumCount> overall = new Dictionary<int, SumCount>();

        private Dictionary<string, Dictionary<int, SumCount>> byGender = new Dictionary<string, Dictionary<int, SumCount>>(StringComparer.OrdinalIgnoreCase);

        private Dictionary<string, Dictionary<int, SumCount>> byGenre = new Dictionary<string, Dictionary<int, SumCount>>(StringComparer.OrdinalIgnoreCase);

        private Dictionary<AgeGroup, Dictionary<int, SumCount>> byAgeGroup = new Dictionary<AgeGroup, Dictionary<int, SumCount>>();

        public AggregateBucket()
        {
            byGender["M"] = new Dictionary<int, SumCount>();
            byGender["F"] = new Dictionary<int, SumCount>();

            byAgeGroup[AgeGroup.Under18] = new Dictionary<int, SumCount>();
            byAgeGroup[AgeGroup.Age18To29] = new Dictionary<int, SumCount>();
            byAgeGroup[AgeGroup.Age30Plus] = new Dictionary<int, SumCount>();
        }

        public void AddRating(int movieId, int rating)
        {
            if (!overall.TryGetValue(movieId, out var sc))
            {
                sc = new SumCount();
                overall[movieId] = sc;
            }
            sc.Add(rating);
        }

        public void AddRatingForGender(int movieId, string gender, int rating)
        {
            if (string.IsNullOrEmpty(gender)) return;
            if (!byGender.TryGetValue(gender, out var dict))
            {
                dict = new Dictionary<int, SumCount>();
                byGender[gender] = dict;
            }
            if (!dict.TryGetValue(movieId, out var sc))
            {
                sc = new SumCount();
                dict[movieId] = sc;
            }
            sc.Add(rating);
        }

        public void AddRatingForGenre(string genre, int movieId, int rating)
        {
            if (string.IsNullOrEmpty(genre)) return;
            if (!byGenre.TryGetValue(genre, out var dict))
            {
                dict = new Dictionary<int, SumCount>();
                byGenre[genre] = dict;
            }
            if (!dict.TryGetValue(movieId, out var sc))
            {
                sc = new SumCount();
                dict[movieId] = sc;
            }
            sc.Add(rating);
        }

        public void AddRatingForAgeGroup(int movieId, AgeGroup ageGroup, int rating)
        {
            if (!byAgeGroup.TryGetValue(ageGroup, out var dict))
            {
                dict = new Dictionary<int, SumCount>();
                byAgeGroup[ageGroup] = dict;
            }
            if (!dict.TryGetValue(movieId, out var sc))
            {
                sc = new SumCount();
                dict[movieId] = sc;
            }
            sc.Add(rating);
        }

        public void Merge(AggregateBucket other)
        {
            foreach (var kv in other.overall)
            {
                if (!overall.TryGetValue(kv.Key, out var sc)) overall[kv.Key] = new SumCount();
                overall[kv.Key].Merge(kv.Value);
            }

            foreach (var genKvp in other.byGender)
            {
                if (!byGender.TryGetValue(genKvp.Key, out var dict)) byGender[genKvp.Key] = new Dictionary<int, SumCount>();
                foreach (var kv in genKvp.Value)
                {
                    if (!byGender[genKvp.Key].TryGetValue(kv.Key, out var sc)) byGender[genKvp.Key][kv.Key] = new SumCount();
                    byGender[genKvp.Key][kv.Key].Merge(kv.Value);
                }
            }

            foreach (var gKvp in other.byGenre)
            {
                if (!byGenre.TryGetValue(gKvp.Key, out var dict)) byGenre[gKvp.Key] = new Dictionary<int, SumCount>();
                foreach (var kv in gKvp.Value)
                {
                    if (!byGenre[gKvp.Key].TryGetValue(kv.Key, out var sc)) byGenre[gKvp.Key][kv.Key] = new SumCount();
                    byGenre[gKvp.Key][kv.Key].Merge(kv.Value);
                }
            }

            foreach (var ag in other.byAgeGroup.Keys)
            {
                if (!byAgeGroup.TryGetValue(ag, out var dict)) byAgeGroup[ag] = new Dictionary<int, SumCount>();
                foreach (var kv in other.byAgeGroup[ag])
                {
                    if (!byAgeGroup[ag].TryGetValue(kv.Key, out var sc)) byAgeGroup[ag][kv.Key] = new SumCount();
                    byAgeGroup[ag][kv.Key].Merge(kv.Value);
                }
            }
        }

        #region Query helpers for top lists
        public IEnumerable<KeyValuePair<int, SumCount>> GetTopMoviesOverall(int n)
        {
            return overall
                .Where(kv => kv.Value.Count > 0)
                .OrderByDescending(kv => kv.Value.Average)
                .ThenByDescending(kv => kv.Value.Count)
                .Take(n);
        }

        public IEnumerable<KeyValuePair<int, SumCount>> GetTopMoviesByGender(string gender, int n)
        {
            if (!byGender.TryGetValue(gender, out var dict)) return Enumerable.Empty<KeyValuePair<int, SumCount>>();
            return dict
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

        public IEnumerable<KeyValuePair<int, SumCount>> GetTopMoviesByAgeGroup(AgeGroup ag, int n)
        {
            if (!byAgeGroup.TryGetValue(ag, out var dict)) return Enumerable.Empty<KeyValuePair<int, SumCount>>();
            return dict
                .Where(kv => kv.Value.Count > 0)
                .OrderByDescending(kv => kv.Value.Average)
                .ThenByDescending(kv => kv.Value.Count)
                .Take(n);
        }
        #endregion
    }
    #endregion
}
