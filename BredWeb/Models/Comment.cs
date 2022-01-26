﻿using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace BredWeb.Models
{
    public class Comment
    {
        [Key]
        public int Id { get; set; }
        [Required]
        [MaxLength(500)]
        public string Body { get; set; }
        public DateTime PostDate { get; set; }
        public bool IsEdited { get; set; } = false;
        public string AuthorName { get; set; }
        //public List<Rating>? RatingList { get; set; } = new();
        //public List<Post>? PostList { get; set; }
        public int Rating { get; set; } = 0;
        public int PostId { get; set; }
    }
}
