using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;

using BookService.Helpers;
using BookService.Models;

using Microsoft.AspNetCore.Mvc;

namespace product_service.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class BooksController : ControllerBase
    {
        private readonly IEnumerable<Book> Books;

        public BooksController()
        {
            Books = BookFactoryHelper.CreateBooks();
        }

        [HttpGet("hostname")]
        public string GetHostName()
        {
            var hostname = Dns.GetHostName();
            var ipaddress = Dns.GetHostAddresses(hostname);

            var ipResult = string.Join("-", ipaddress.Select(x => x.ToString()));

            var result = $"MachineNae: {Environment.MachineName} - HostName: {hostname} - IPAddress: {ipResult}";

            return result;
        }

        [HttpGet()]
        public IEnumerable<Book> Get()
        {
            return Books;
        }

        [HttpGet("{code:guid}")]
        public Book Get(Guid code)
        {
            return Books.Where(b => b.Code == code).SingleOrDefault();
        }
    }
}