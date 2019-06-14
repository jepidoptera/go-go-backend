using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Newtonsoft.Json;

namespace GoGoBackend.Controllers
{
    [Route("api/[controller]")]
    public class SecretsController : Controller
    {
		public static IConfiguration configuration;

		// i think we're getting the config variables from appsettings.json here
		// by requesting a service which has been declared in startup.cs
		// never have been given a good explanation of how this works, unfortunately
		public SecretsController(IConfiguration iconfig)
		{
			// right. it's injecting the app configuration object
			configuration = iconfig;
		}

		public static string SecretKey(string key)
		{
			// get the requested value out of the environment variable where it's stored
			// this is much better than my previous approach of storing them in a plaintext dictionary
			return configuration.GetSection("Secret").GetSection(key).Value;
		}

		public static string ConnString { get {
			// return the string used to connect to the database
			return "Server=tcp:gogobackend.database.windows.net,1433;Initial Catalog = user_registry; Persist Security Info=False;User ID = Jepidoptera; Password=" +
			configuration.GetSection("secrets").GetSection("databasePassword").Value + "; MultipleActiveResultSets=False;Encrypt=True;TrustServerCertificate=False;Connection Timeout = 30;";
		} }

		// GET api/values
		[HttpGet]
        public IEnumerable<string> Get()
        {
            return new string[] { "Spilling all secrets now!", configuration.GetSection("secrets").GetSection("databasePassword").Value };
        }

        // GET api/values/5
        [HttpGet("{id}")]
        public string Get(int id)
        {
            return "value";
        }

        // POST api/values
        [HttpPost]
        public void Post([FromBody]string value)
        {
        }

        // PUT api/values/5
        [HttpPut("{id}")]
        public void Put(int id, [FromBody]string value)
        {
        }

        // DELETE api/values/5
        [HttpDelete("{id}")]
        public void Delete(int id)
        {
        }
    }
}
