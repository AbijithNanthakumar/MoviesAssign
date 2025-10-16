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

        // Load movies.dat and ratings.dat from the folder
        public static MovieLensData LoadFromFolder(string folderPath)
        {
            var data = new MovieLensData();

            string moviesFile = Path.Combine(folderPath, "movies.dat");
            string ratingsFile = Path.Combine(folderPath, "ratings.dat");

            if (!File.Exists(moviesFile)) throw new FileNotFoundException($"Missing file: {moviesFile}");
            if (!File.Exists(ratingsFile)) throw new FileNotFoundException($"Missing file: {ratingsFile}");

            // movies.dat format: MovieID::Title::Genres (genres pipe-separated)
            foreach (var raw in File.ReadLines(moviesFile))
            {
                if (string.IsNullOrWhiteSpace(raw)) continue;
                // Some titles may contain '::' so split only first two separators
                var parts = raw.Split(new string[] { "::" }, StringSplitOptions.None);
                if (parts.Length < 3) continue;
                if (!int.TryParse(parts[0], out int movieId)) continue;
                var title = parts[1];
                var genresField = parts[2];
                var genres = genresField.Split(new char[] { '|' }, StringSplitOptions.RemoveEmptyEntries);

                var movie = new Movie
                {
                    MovieId = movieId,
                    Title = title,
                    Genres = genres.Select(g => g.Trim()).Where(g => !string.IsNullOrEmpty(g)).ToList()
                };
                data.Movies[movieId] = movie;
            }

            // ratings.dat format: UserID::MovieID::Rating::Timestamp
            // Ratings may be decimal (e.g., 4.5). Use invariant culture.
            foreach (var line in File.ReadLines(ratingsFile))
            {
                if (string.IsNullOrWhiteSpace(line)) continue;
                var parts = line.Split(new string[] { "::" }, StringSplitOptions.None);
                if (parts.Length < 4) continue;
                if (!int.TryParse(parts[0], out int userId)) continue;
                if (!int.TryParse(parts[1], out int movieId)) continue;
                if (!double.TryParse(parts[2], NumberStyles.Float, CultureInfo.InvariantCulture, out double ratingVal)) continue;
                if (!long.TryParse(parts[3], out long ts)) ts = 0;

                var rating = new Rating
                {
                    UserId = userId,
                    MovieId = movieId,
                    Value = ratingVal,
                    Timestamp = ts
                };
                data.Ratings.Add(rating);
            }

            return data;
        }
    }
}
