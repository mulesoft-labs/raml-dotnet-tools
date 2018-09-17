// Template: Models (ApiModel.t4) version 3.0

// This code was generated by RAML Server Scaffolder

using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using System.ComponentModel.DataAnnotations;

namespace ChinookAspNetCore.ChinookV1.Models
{
    public partial class Album
    {
        

        [Required]
        public object Artist { get; set; }

        [Required]
        [MaxLength(0)]
        [MinLength(0)]
        public int Id { get; set; }

        [Required]
        [MaxLength(0)]
        [MinLength(0)]
        public string Title { get; set; }
    } // end class

} // end Models namespace

