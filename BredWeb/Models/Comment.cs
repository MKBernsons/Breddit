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
        public string Body { get; set; } = string.Empty;
        public DateTime PostDate { get; set; }
        public string AuthorName { get; set; } = string.Empty;
        public int PostId { get; set; }
    }
}
