using Microsoft.AspNetCore.Mvc;
using ProductAnalysisApp.Entities.DataTransferObjects;
using ProductAnalysisApp.Entities.Models;
using ProductAnalysisApp.Services.Contracts;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ProductAnalysisApp.Presentation.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class ProductController : ControllerBase
    {
        private readonly IServiceManager _manager;

        public ProductController(IServiceManager manager)
        {
            _manager = manager;
        }

        [HttpGet]
        public IActionResult GetAllProducts()
        {
            var list = _manager.ProductService.GetAllProducts();
            return Ok(list);
        }

        [HttpGet("{id}")]
        public IActionResult GetOneProduct(string id)
        {
            var product = _manager.ProductService.GetOneProduct(id);
            if (product == null) return NotFound();
            return Ok(product);
        }

        [HttpPost]
        public async Task<IActionResult> AddOneProduct([FromBody] ProductDtoForCreate dto)
        {
            if (dto == null) return BadRequest();

            var product = await _manager.ProductService.AddOneProductAsync(dto);
            return StatusCode(201, product);
        }

        [HttpPut("{id}")]
        public async Task<IActionResult> UpdateOneProduct(string id, [FromBody] Product product)
        {
            await _manager.ProductService.UpdateOneProductAsync(id, product);
            return NoContent();
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteOneProduct(string id)
        {
            await _manager.ProductService.DeleteOneProductAsync(id);
            return NoContent();
        }
    }
}
