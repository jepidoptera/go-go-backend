using System;
using System.Collections.Generic;
using System.Configuration.Assemblies;
using Microsoft.Extensions.Configuration;

namespace SecretKeys
{
    // todo: put these in a key vault
    public class Secrets
    {

		public static string key(string secretName)
		{
			return "shhhhh";
			//return IConfiguration;
		}
	}
}

