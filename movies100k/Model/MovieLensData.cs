using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Model
{
    public class MovieLensData
    {
        public Dictionary<int, Movie> Movies { get; private set; } = new Dictionary<int, Movie>();
        public List<Rating> Ratings { get; private set; } = new List<Rating>();
        public Dictionary<int, User> Users { get; private set; } = new Dictionary<int, User>();

        

        public static MovieLensData LoadFromFolder(string folderPath)
        {
            var data = new MovieLensData();

            string uData = Path.Combine(folderPath, "u.data");
            string uItem = Path.Combine(folderPath, "u.item");
            string uUser = Path.Combine(folderPath, "u.user");

            if (!File.Exists(uData)) throw new FileNotFoundException($"File not found: {uData}");
            if (!File.Exists(uItem)) throw new FileNotFoundException($"File not found: {uItem}");
            if (!File.Exists(uUser)) throw new FileNotFoundException($"File not found: {uUser}");

            foreach (var line in File.ReadLines(uUser))
            {
                if (string.IsNullOrWhiteSpace(line)) continue;
                var parts = line.Split('|');
                if (parts.Length < 5) continue;
                if (!int.TryParse(parts[0], out int userId)) continue;
                var user = new User
                {
                    UserId = userId,
                    Age = int.TryParse(parts[1], out int age) ? age : 0,
                    Gender = parts[2],
                    Occupation = parts[3],
                    ZipCode = parts[4]
                };
                data.Users[userId] = user;
            }

           
            var genreNames = new string[]
            {
                "Unknown","Action","Adventure","Animation","Children's","Comedy","Crime","Documentary",
                "Drama","Fantasy","Film-Noir","Horror","Musical","Mystery","Romance","Sci-Fi","Thriller",
                "War","Western"
            };

            foreach (var line in File.ReadLines(uItem))
            {
                if (string.IsNullOrWhiteSpace(line)) continue;
                var parts = line.Split('|');
                if (parts.Length < 6) continue;
                if (!int.TryParse(parts[0], out int movieId)) continue;
                string title = parts[1];

                var movie = new Movie
                {
                    MovieId = movieId,
                    Title = title
                };

                int startIndexForGenres = Math.Max(0, parts.Length - 19);
                for (int i = 0; i < 19 && startIndexForGenres + i < parts.Length; i++)
                {
                    if (parts[startIndexForGenres + i] == "1")
                    {
                        movie.Genres.Add(genreNames[i]);
                    }
                }

                data.Movies[movieId] = movie;
            }

            foreach (var line in File.ReadLines(uData))
            {
                if (string.IsNullOrWhiteSpace(line)) continue;
                var parts = line.Split(new char[] { '\t', ' ', ',' }, StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length < 4) continue;
                if (!int.TryParse(parts[0], out int userId)) continue;
                if (!int.TryParse(parts[1], out int movieId)) continue;
                if (!int.TryParse(parts[2], out int ratingVal)) continue;
                if (!long.TryParse(parts[3], out long timestamp)) timestamp = 0;

                var rating = new Rating
                {
                    UserId = userId,
                    MovieId = movieId,
                    Value = ratingVal,
                    Timestamp = timestamp
                };
                data.Ratings.Add(rating);
            }

            return data;
        }
    }
}
