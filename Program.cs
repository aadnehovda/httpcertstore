/*
 * This is a simple implementation of a Caddy compatible certificate manager that gets certificates
 * and keys out of the Windows Certificate Store.
 *
 * Run the program and configure Caddy and automatically fetch its certificates straight from the Windows
 * Certificate store.
 *
 * {
 *   auto_https off
 * }
 * test.local.gd {
 *   tls {
 *     get_certificate http http://127.0.0.1:27510/LocalMachine/My
 *   }
 *   respond "Hello, world!"
 * }
 *
 * You can let caddy fetch certs from a different store by using a different url,
 * e.g. http://127.0.0.1:27510/CurrentUser/My
 *
 * The api endpoint will receive the hostname, signature algorithms and ciphers from 
 * caddy's request, look up a matching valid certificate, and dump it back to caddy
 * as plain text.
 *
 * https://caddyserver.com/docs/caddyfile/directives/tls#http
 *
 */
using System.Globalization;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using Microsoft.AspNetCore.Mvc;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.WebHost.UseUrls("http://127.0.0.1:27510");
var app = builder.Build();

app.UseSwaggerUI();
app.UseSwagger();

//app.MapGet("/", () =>
//{
//	//return Results.Text("");
//});


app
	.MapGet("/certificate/{name}/{location}", (
		[FromRoute] string? name,
		[FromRoute] string? location,
		[FromQuery(Name = "server_name")] string? host,
		[FromQuery(Name = "signature_schemes")] string? schemes,
		[FromQuery(Name = "cipher_suites")] string? suites) =>
	{
		if (!Enum.TryParse(name, true, out StoreName storeName))
			storeName = StoreName.My;

		if (!Enum.TryParse(location, true, out StoreLocation storeLocation))
			storeLocation = StoreLocation.LocalMachine;

		var store = new X509Store(storeName, storeLocation, OpenFlags.OpenExistingOnly | OpenFlags.ReadOnly);

		var signatureAlgorithms = schemes?
			.Split(",", StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
			.Select(a => uint.Parse(a, NumberStyles.HexNumber));

		var ciphers = suites?
			.Split(",", StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
			.Select(a => uint.Parse(a, NumberStyles.HexNumber));

		foreach (var cert in store
					 .Certificates
					 .Where(c => c.HasPrivateKey && host != null && c.MatchesHostname(host))
					 .OrderByDescending(c => c.NotAfter))
		{
			var chain = X509Chain.Create();
			chain.Build(cert);

			var c = chain.ChainElements.Select(c => c.Certificate.ExportCertificatePem());
			c = c.Prepend(GetPrivateKey(cert));
			return Results.Content(string.Join(Environment.NewLine, c), "application/pem-certificate-chain");
		}

		return Results.NotFound($"Could not find certificate for {host}");
	})
	.WithOpenApi();

app.Run();

static string GetPrivateKey(X509Certificate2 cert)
{
	if (cert.PublicKey.Oid.Value == "1.2.840.113549.1.1.1" && cert.GetRSAPrivateKey() is RSACng rsa)
	{
		MakeExportable(rsa.Key);
		return rsa.ExportRSAPrivateKeyPem();
	}

	if (cert.PublicKey.Oid.Value == "1.2.840.10040.4.1" && cert.GetDSAPrivateKey() is DSACng dsa)
	{
		MakeExportable(dsa.Key);
		return dsa.ExportPkcs8PrivateKeyPem();
	}

	if (cert.PublicKey.Oid.Value == "1.2.840.10045.2.1" && cert.GetECDsaPrivateKey() is ECDsaCng ecdsa)
	{
		MakeExportable(ecdsa.Key);
		return ecdsa.ExportECPrivateKeyPem();
	}

	throw new Exception($"Unable to get private key from certificate {cert}.");
}

static void MakeExportable(CngKey key)
{
	if ((key.ExportPolicy & CngExportPolicies.AllowPlaintextExport) == CngExportPolicies.AllowPlaintextExport)
		return;

	var exportable = new CngProperty(
		"Export Policy",
		BitConverter.GetBytes((int)CngExportPolicies.AllowPlaintextExport),
		CngPropertyOptions.None);

	key.SetProperty(exportable);
}
