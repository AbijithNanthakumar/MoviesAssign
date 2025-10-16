using System;
using System.Collections.Generic;

namespace Model
{
    public class Movie
    {
        public int MovieId { get; set; }
        public string Title { get; set; }
        public List<string> Genres { get; set; } = new List<string>();
    }

    public class Rating
    {
        public int UserId { get; set; }
        public int MovieId { get; set; }
        public int Value { get; set; } 
        public long Timestamp { get; set; }
    }

    public class User
    {
        public int UserId { get; set; }
        public int Age { get; set; }
        public string Gender { get; set; } 
        public string Occupation { get; set; }
        public string ZipCode { get; set; }
    }
}
