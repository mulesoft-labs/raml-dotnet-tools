// Template: Base Controller (ApiControllerBase.t4) version 3.0

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using ChinookAspNetCore.ChinookV1.Models;

// Do not modify this file. This code was generated by RAML Server Scaffolder

namespace ChinookAspNetCore.ChinookV1
{
    [Route("customers")]
    public partial class CustomersController : Controller
    {


/// <summary>
		/// /customers
		/// </summary>
		/// <returns>IList&lt;Customer&gt;</returns>
        [HttpGet("customers")]
        public virtual IActionResult GetBase()
        {
            // Do not modify this code
                        return  ((ICustomersController)this).Get();
                    }

/// <summary>
		/// /customers
		/// </summary>
		/// <param name="customer"></param>
        [HttpPost("customers")]
        public virtual IActionResult PostBase([FromBody] Models.Customer customer)
        {
            // Do not modify this code
                        return  ((ICustomersController)this).Post(customer);
                    }

/// <summary>
		/// /customers/{id}
		/// </summary>
		/// <param name="id"></param>
		/// <returns>Customer</returns>
        [HttpGet("{id}")]
        public virtual IActionResult GetByIdBase(string id)
        {
            // Do not modify this code
                        return  ((ICustomersController)this).GetById(id);
                    }

/// <summary>
		/// /customers/{id}
		/// </summary>
		/// <param name="customer"></param>
		/// <param name="id"></param>
        [HttpPut("{id}")]
        public virtual IActionResult PutBase([FromBody] Models.Customer customer,string id)
        {
            // Do not modify this code
                        return  ((ICustomersController)this).Put(customer,id);
                    }

/// <summary>
		/// /customers/{id}
		/// </summary>
		/// <param name="id"></param>
        [HttpDelete("{id}")]
        public virtual IActionResult DeleteBase(string id)
        {
            // Do not modify this code
                        return  ((ICustomersController)this).Delete(id);
                    }
    }
}
