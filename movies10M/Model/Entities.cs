using System.Collections.Generic;

namespace Model
{
    public class Movie
    {
        public int MovieId { get; set; }
        public string Title { get; set; }
        // genres parsed from movies.dat (pipe-separated)
        public List<string> Genres { get; set; } = new List<string>();
    }

    public class Rating
    {
        public int UserId { get; set; }
        public int MovieId { get; set; }
        // rating in MovieLens 10M can be fractional (e.g., 4.5)
        public double Value { get; set; }
        public long Timestamp { get; set; }
    }
}
