// Template: Models (ApiModel.t4) version 0.1

// This code was generated by RAML Web Api 2 Scaffolder

using System;
using System.Collections.Generic;
using Newtonsoft.Json;

namespace FstabExplorerTest.Fstab.Models
{
    public partial class Storagetmpfs  : Storage
    {
        
		[JsonProperty("sizeInMB")]
        public int SizeInMB { get; set; }
    } // end class

} // end Models namespace
