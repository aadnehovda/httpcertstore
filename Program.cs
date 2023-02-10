using Microsoft.AspNetCore.Mvc;
using System.Globalization;
using System.Net.Security;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddOutputCache();
builder.Services.AddResponseCompression();
builder.Services.ConfigureHttpJsonOptions(options => options.SerializerOptions.WriteIndented = true);
builder.Services.AddProblemDetails();

var app = builder.Build();
app.UseOutputCache();
app.UseResponseCompression();
app.UseStatusCodePages();


app.MapGet("/",
	(HttpRequest req) => $$"""
This is a simple implementation of a Caddy compatible certificate manager 
that gets certificates and keys out of the Windows Certificate Store.

Run the program and configure Caddy like this: 

{
  auto_https off
}

test.local.gd {
  tls {
    get_certificate http {{req.Scheme}}{{Uri.SchemeDelimiter}}{{req.Host}}/LocalMachine/My
  }

  respond "Hello, world!"
}

You can search for certs in the current user's store too
* {{req.Scheme}}{{Uri.SchemeDelimiter}}{{req.Host}}/CurrentUser/My

If you append the thumbprint of the certificate to the end it will try to 
export it including the private key, similarly to how caddy will see it.

Caddy will append some query string parameters which will be used to 
find a matching certificate:

* server_name
* signature_schemes
* cipher_suites

The api endpoint will receive the hostname, signature algorithms and ciphers from 
caddy's request, look up a matching valid certificate, and dump it back to caddy
as plain text.

https://caddyserver.com/docs/caddyfile/directives/tls#http

TODO: validate the signature schemes (and ciphers) when looking for a supported certificate.

""");


app.MapGet("{location}/{name}/{thumbprint}", (
	StoreLocation location,
	string name,
	string thumbprint) =>
{
	using var store = new X509Store(name, location);
	store.Open(OpenFlags.OpenExistingOnly | OpenFlags.ReadOnly);
	foreach (var cert in store.Certificates.Find(X509FindType.FindByThumbprint, thumbprint, false))
	{
		try
		{
			return Results.Text(GetFullPem(cert));
		}
		catch (Exception e)
		{
			return Results.BadRequest(e.Message);
		}
	}

	return Results.NotFound(thumbprint);
});

app.MapGet("{location}/{name:alpha}", (
	[FromRoute] StoreLocation location,
	string name,
	[FromQuery] string? server_name
	//, [FromQuery(Name = "signature_schemes")] Signatures? schemes
	//, [FromQuery(Name = "cipher_suites")] CipherSuites? suites
	) =>
{
	using var store = new X509Store(name, location);
	store.Open(OpenFlags.OpenExistingOnly | OpenFlags.ReadOnly);

	if (server_name == null)
	{
		var descriptions = store
			.Certificates
			.Select(
				c =>
				{
					var sans = c.Extensions
						.OfType<X509SubjectAlternativeNameExtension>()
						.SelectMany(e => e.EnumerateDnsNames())
						.ToList();

					return new
					{
						c.Thumbprint,
						Subject = c.SubjectName.Name,
						Expires = c.NotAfter,
						Signature = c.SignatureAlgorithm,
						PublicKeyAlgorithm = c.PublicKey.Oid,
						SubjectAlternativeName = sans
						//, PrivateKeyExportPolicies = GetExportPolicies(GetPrivateCngKey(c))
					};
				});
		return Results.Ok(descriptions);
	}

	var pem = GetUsableCertificates(store.Certificates)
		.Where(c => c.MatchesHostname(server_name))
		.MaxBy(c => c.NotAfter);

	if (pem != null)
		try
		{
			return Results.Text(GetFullPem(pem));
		}
		catch (Exception e)
		{
			return Results.NotFound(e.Message);
		}

	return Results.NotFound($"Could not find certificate for {server_name}");
});


app.Run("http://127.0.0.1:27510");

static IEnumerable<string> GetExportPolicies(CngKey? key)
{
	return Enum.GetValues<CngExportPolicies>()
		.Where(p => key != null && key.ExportPolicy.HasFlag(p))
		.Select(p => p.ToString());
}

static IEnumerable<X509Certificate2> GetUsableCertificates(X509Certificate2Collection certs) =>
	certs.Where(c => IsCertificateAllowedForServerAuth(c) && c.HasPrivateKey);

static string GetFullPem(X509Certificate2 cert)
{
	var chain = X509Chain.Create();
	chain.Build(cert);

	var builder = new StringBuilder();

	builder.AppendLine(GetPrivateKey(cert));


	builder.AppendLine(cert.ToString(true));
	foreach (var c in chain.ChainElements)
	{
		builder.AppendLine(c.Certificate.ExportCertificatePem());
	}

	return builder.ToString();
}


static CngKey? GetPrivateCngKey(X509Certificate2 cert)
{
	try
	{
		if (cert.GetRSAPrivateKey() is RSACng rsa)
		{
			return rsa.Key;
		}

		if (cert.GetDSAPrivateKey() is DSACng dsa)
		{
			return dsa.Key;
		}

		if (cert.GetECDsaPrivateKey() is ECDsaCng ecd)
		{
			return ecd.Key;
		}

		if (cert.GetECDiffieHellmanPrivateKey() is ECDiffieHellmanCng ecdh)
		{
			return ecdh.Key;
		}
	}
	catch (CryptographicException e)
	{
		throw new Exception($"Unable to export private key from certificate {cert.Subject}.", e);
	}

	return default;
}

static string GetPrivateKey(X509Certificate2 cert, bool makeexportable = false)
{
	try
	{
		if (cert.GetRSAPrivateKey() is RSACng rsa)
		{
			if (makeexportable) MakeExportable(rsa.Key);
			return rsa.ExportRSAPrivateKeyPem();
		}

		if (cert.GetDSAPrivateKey() is DSACng dsa)
		{
			if (makeexportable) MakeExportable(dsa.Key);
			return dsa.ExportPkcs8PrivateKeyPem();
		}

		if (cert.GetECDsaPrivateKey() is ECDsaCng ecd)
		{
			if (makeexportable) MakeExportable(ecd.Key);
			return ecd.ExportECPrivateKeyPem();
		}

		if (cert.GetECDiffieHellmanPrivateKey() is ECDiffieHellmanCng ecdh)
		{
			if (makeexportable) MakeExportable(ecdh.Key);
			return ecdh.ExportECPrivateKeyPem();
		}
	}
	catch (CryptographicException e)
	{
		throw new Exception($"Unable to export private key from certificate {cert.Subject}.", e);
	}

	throw new Exception($"Unable to export private key from certificate {cert.Subject}.");
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

// lifted from Kestrel...
static bool IsCertificateAllowedForServerAuth(X509Certificate2 certificate)
{
	/* If the Extended Key Usage extension is included, then we check that the serverAuth usage is included. (http://oid-info.com/get/1.3.6.1.5.5.7.3.1)
	 * If the Extended Key Usage extension is not included, then we assume the certificate is allowed for all usages.
	 *
	 * See also https://blogs.msdn.microsoft.com/kaushal/2012/02/17/client-certificates-vs-server-certificates/
	 *
	 * From https://tools.ietf.org/html/rfc3280#section-4.2.1.13 "Certificate Extensions: Extended Key Usage"
	 *
	 * If the (Extended Key Usage) extension is present, then the certificate MUST only be used
	 * for one of the purposes indicated.  If multiple purposes are
	 * indicated the application need not recognize all purposes indicated,
	 * as long as the intended purpose is present.  Certificate using
	 * applications MAY require that a particular purpose be indicated in
	 * order for the certificate to be acceptable to that application.
	 */

	var hasEkuExtension = false;

	foreach (var extension in certificate.Extensions.OfType<X509EnhancedKeyUsageExtension>())
	{
		hasEkuExtension = true;
		foreach (var oid in extension.EnhancedKeyUsages)
		{
			if (string.Equals(oid.Value, "1.3.6.1.5.5.7.3.1", StringComparison.Ordinal))
			{
				return true;
			}
		}
	}

	return !hasEkuExtension;
}


internal class Signatures : List<Oid>
{
	private static HashAlgorithmName HashFromTlsId(HashAlgorithm id)
	{
		return id switch
		{
			HashAlgorithm.md5 => HashAlgorithmName.MD5,
			HashAlgorithm.sha1 => HashAlgorithmName.SHA1,
			HashAlgorithm.sha256 => HashAlgorithmName.SHA256,
			HashAlgorithm.sha384 => HashAlgorithmName.SHA384,
			HashAlgorithm.sha512 => HashAlgorithmName.SHA512,
			_ => throw new CryptographicException(
				$"Cannot translate TLS signature hash ID {id} to a known hash algorithm.")
		};
	}

	private static Oid FromTlsSignatureIndex(ushort tls_sig_hash)
	{
		var r = tls_sig_hash switch
		{
			0x0401 => "sha256RSA",
			0x0501 => "sha384RSA",
			0x0601 => "sha512RSA"
		};

		var hash = (HashAlgorithm)(tls_sig_hash >> 8);
		var sig = GetSignatureAlgorithm((byte)tls_sig_hash);

		var friendlyName = $"{Enum.GetName(typeof(HashAlgorithm), hash)}{sig}";

		try
		{
			var oid = Oid.FromFriendlyName(friendlyName, OidGroup.All);
			return oid;
		}
		catch (Exception e)
		{
			return null;
		}
	}

	private enum HashAlgorithm : byte
	{
		none = 0,
		md5 = 1,
		sha1 = 2,
		sha224 = 3,
		sha256 = 4,
		sha384 = 5,
		sha512 = 6
	}

	private static string GetSignatureAlgorithm(byte alg)
	{
		return alg switch
		{
			0x01 => "RSA",
			0x02 => "DSA",
			0x03 => "ECDSA",
			0x08 => "RSASSA-PSS",
			_ => throw new ArgumentException($"Unknown algorithm {alg}")
		};
	}

	public static bool TryParse(string input, out Signatures output)
	{
		output = new Signatures();
		output.AddRange(input
			.Split(",", StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
			.Select(a => ushort.Parse(a, NumberStyles.HexNumber))
			.Select(FromTlsSignatureIndex));

		return true;
	}
}


internal class CipherSuites : List<TlsCipherSuite>
{


	public static bool TryParse(string input, out CipherSuites output)
	{
		output = new CipherSuites();
		output.AddRange(input
			.Split(",", StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
			.Select(a => ushort.Parse(a, NumberStyles.HexNumber))
			.Where(n => Enum.IsDefined(typeof(TlsCipherSuite), n))
			.Cast<TlsCipherSuite>());

		return true;
	}
}
