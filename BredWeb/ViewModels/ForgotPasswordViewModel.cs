﻿using System.ComponentModel.DataAnnotations;

namespace BredWeb.Models
{
    public class ForgotPasswordViewModel
    {
        [Required]
        [EmailAddress]
        public string Email { get; set; } = string.Empty;
    }
}
