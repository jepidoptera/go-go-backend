using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Belgrade.SqlClient;
using Belgrade.SqlClient.SqlDb;
using System.Data.SqlClient;

namespace GoGoBackend
{
    public class Startup
    {
		public const string serverName = "gogobackend.database.windows.net";
		public const string apiServer = "https://gogobackend.azurewebsites.net";
		public const string ConnString = // "Server=" + serverName + ";Database=user_registry;User Id=Jepidoptera;Password=Gogopopterix-Billy";
		"Server=tcp:" + serverName + ",1433;Initial Catalog = user_registry; Persist Security Info=False;User ID = Jepidoptera; Password=" +
			"Gogopopterix-Billy; MultipleActiveResultSets=False;Encrypt=True;TrustServerCertificate=False;Connection Timeout = 30;";

	public Startup(IConfiguration configuration)
        {
            Configuration = configuration;
        }

        public IConfiguration Configuration { get; }

		// This method gets called by the runtime. Use this method to add services to the container.
		public void ConfigureServices(IServiceCollection services)
		{


			services.AddTransient<IQueryPipe>(_ => new QueryPipe(new SqlConnection(ConnString)));
			services.AddTransient<ICommand>(_ => new Command(new SqlConnection(ConnString)));

			// Add framework services.
			services.AddMvc();

			// add configuration service
			services.AddSingleton<IConfiguration>(Configuration);
		}

		// This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
		public void Configure(IApplicationBuilder app, IHostingEnvironment env)
        {
            if (env.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
            }

            app.UseMvc();
        }
    }
}
