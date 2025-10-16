using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Globalization;

namespace Model
{
    public class MovieLensData
    {
        public Dictionary<int, Movie> Movies { get; private set; } = new Dictionary<int, Movie>();
        public List<Rating> Ratings { get; private set; } = new List<Rating>();

        public static MovieLensData LoadFromFolder(string folderPath)
        {
            var data = new MovieLensData();

            string moviesFile = Path.Combine(folderPath, "movies.csv");
            string ratingsFile = Path.Combine(folderPath, "ratings.csv");

            if (!File.Exists(moviesFile)) throw new FileNotFoundException($"Missing: {moviesFile}");
            if (!File.Exists(ratingsFile)) throw new FileNotFoundException($"Missing: {ratingsFile}");

            foreach (var line in File.ReadLines(moviesFile))
            {
                if (string.IsNullOrWhiteSpace(line)) continue;
                var parts = line.Split(new string[] { "::" }, StringSplitOptions.None);
                if (parts.Length < 3) continue;
                if (!int.TryParse(parts[0], out int movieId)) continue;

                var title = parts[1];

                var genres = parts[2]
                    .Split(new char[] { '|' }, StringSplitOptions.RemoveEmptyEntries)
                    .Select(g => g.Trim())
                    .ToList();

                data.Movies[movieId] = new Movie { MovieId = movieId, Title = title, Genres = genres };
            }

            foreach (var line in File.ReadLines(ratingsFile))
            {
                if (string.IsNullOrWhiteSpace(line)) continue;
                var parts = line.Split(new string[] { "::" }, StringSplitOptions.None);
                if (parts.Length < 4) continue;

                if (!int.TryParse(parts[0], out int userId)) continue;
                if (!int.TryParse(parts[1], out int movieId)) continue;
                if (!double.TryParse(parts[2], NumberStyles.Float, CultureInfo.InvariantCulture, out double rating)) continue;
                if (!long.TryParse(parts[3], out long ts)) ts = 0;

                data.Ratings.Add(new Rating { UserId = userId, MovieId = movieId, Value = rating, Timestamp = ts });
            }

            return data;
        }
    }
}
