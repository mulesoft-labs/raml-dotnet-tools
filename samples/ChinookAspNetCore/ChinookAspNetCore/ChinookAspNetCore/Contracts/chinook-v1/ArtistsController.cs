// Template: Base Controller (ApiControllerBase.t4) version 3.0

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using ChinookAspNetCore.ChinookV1.Models;

// Do not modify this file. This code was generated by RAML Server Scaffolder

namespace ChinookAspNetCore.ChinookV1
{
    [Route("artists")]
    public partial class ArtistsController : Controller
    {


/// <summary>
		/// /artists
		/// </summary>
		/// <returns>IList&lt;Artist&gt;</returns>
        [HttpGet("artists")]
        public virtual IActionResult GetBase()
        {
            // Do not modify this code
                        return  ((IArtistsController)this).Get();
                    }

/// <summary>
		/// /artists
		/// </summary>
		/// <param name="artist"></param>
        [HttpPost("artists")]
        public virtual IActionResult PostBase([FromBody] Models.Artist artist)
        {
            // Do not modify this code
                        return  ((IArtistsController)this).Post(artist);
                    }

/// <summary>
		/// /artists/{id}
		/// </summary>
		/// <param name="id"></param>
		/// <returns>Artist</returns>
        [HttpGet("{id}")]
        public virtual IActionResult GetByIdBase(string id)
        {
            // Do not modify this code
                        return  ((IArtistsController)this).GetById(id);
                    }

/// <summary>
		/// /artists/{id}
		/// </summary>
		/// <param name="artist"></param>
		/// <param name="id"></param>
        [HttpPut("{id}")]
        public virtual IActionResult PutBase([FromBody] Models.Artist artist,string id)
        {
            // Do not modify this code
                        return  ((IArtistsController)this).Put(artist,id);
                    }

/// <summary>
		/// /artists/{id}
		/// </summary>
		/// <param name="id"></param>
        [HttpDelete("{id}")]
        public virtual IActionResult DeleteBase(string id)
        {
            // Do not modify this code
                        return  ((IArtistsController)this).Delete(id);
                    }

/// <summary>
		/// /artists/bytrack/{id}
		/// </summary>
		/// <param name="id"></param>
		/// <returns>ArtistByTrack</returns>
        [HttpGet("bytrack/{id}")]
        public virtual IActionResult GetBytrackByIdBase(string id)
        {
            // Do not modify this code
                        return  ((IArtistsController)this).GetBytrackById(id);
                    }
    }
}
