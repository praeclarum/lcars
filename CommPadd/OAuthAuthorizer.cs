//
// OAuth framework for TweetStation
//
// Author;
//   Miguel de Icaza (miguel@gnome.org)
//
// Possible optimizations:
//   Instead of sorting every time, keep things sorted
//   Reuse the same dictionary, update the values
//

using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Globalization;
using System.Text;
using System.Net;
using System.Security.Cryptography;
using MonoTouch.UIKit;
using MonoTouch.Foundation;

namespace CommPadd
{
	//
	// Configuration information for an OAuth client
	//
	public class OAuthConfig {
		// keys, callbacks
		public string ConsumerKey, Callback, ConsumerSecret;
		
		// Urls
		public string RequestTokenUrl, AccessTokenUrl, AuthorizeUrl;
	}
		
	//
	// The authorizer uses a config and an optional xAuth user/password
	// to perform the OAuth authorization process as well as signing
	// outgoing http requests
	//
	// To get an access token, you use these methods in the workflow:
	// 	  AcquireRequestToken
	//    AuthorizeUser
	//
	// These static methods only require the access token:
	//    AuthorizeRequest
	//    AuthorizeTwitPic
	//
	public class OAuthAuthorizer {
		
		OAuthConfig config;

		public string RequestToken { get; private set; }
		public string RequestTokenSecret { get; private set; }
		
		public string AccessToken { get; private set; }
		public string AccessTokenSecret { get; private set; }
		
		public string UserId { get; private set; }
		public string ScreenName { get; private set; }
		
		// Constructor for standard OAuth
		public OAuthAuthorizer (OAuthConfig config)
		{
			this.config = config;
		}
		
		static Random random = new Random ();
		static DateTime UnixBaseTime = new DateTime (1970, 1, 1);

		// 16-byte lower-case or digit string
		static string MakeNonce ()
		{
			var ret = new char [16];
			for (int i = 0; i < ret.Length; i++){
				int n = random.Next (35);
				if (n < 10)
					ret [i] = (char) (n + '0');
				else
					ret [i] = (char) (n-10 + 'a');
			}
			return new string (ret);
		}
		
		static string MakeTimestamp ()
		{
			return ((long) (DateTime.UtcNow - UnixBaseTime).TotalSeconds).ToString ();
		}
		
		// Makes an OAuth signature out of the HTTP method, the base URI and the headers
		static string MakeSignature (string method, string base_uri, Dictionary<string,string> headers)
		{
			var items = from k in headers.Keys orderby k 
				select k + "%3D" + OAuth.PercentEncode (headers [k]);

			return method + "&" + OAuth.PercentEncode (base_uri) + "&" + 
				string.Join ("%26", items.ToArray ());
		}
		
		static string MakeSigningKey (string consumerSecret, string oauthTokenSecret)
		{
			return OAuth.PercentEncode (consumerSecret) + "&" + (oauthTokenSecret != null ? OAuth.PercentEncode (oauthTokenSecret) : "");
		}
		
		static string MakeOAuthSignature (string compositeSigningKey, string signatureBase)
		{
			var sha1 = new HMACSHA1 (Encoding.UTF8.GetBytes (compositeSigningKey));
			
			return Convert.ToBase64String (sha1.ComputeHash (Encoding.UTF8.GetBytes (signatureBase)));
		}
		
		static string HeadersToOAuth (Dictionary<string,string> headers)
		{
			return "OAuth " + String.Join (",", (from x in headers.Keys select String.Format ("{0}=\"{1}\"", x, headers [x])).ToArray ());
		}
		
		public bool AcquireRequestToken ()
		{
			var headers = new Dictionary<string,string> () {
				{ "oauth_callback", OAuth.PercentEncode (config.Callback) },
				{ "oauth_consumer_key", config.ConsumerKey },
				{ "oauth_nonce", MakeNonce () },
				{ "oauth_signature_method", "HMAC-SHA1" },
				{ "oauth_timestamp", MakeTimestamp () },
				{ "oauth_version", "1.0" }};
				
			string signature = MakeSignature ("POST", config.RequestTokenUrl, headers);
			string compositeSigningKey = MakeSigningKey (config.ConsumerSecret, null);
			string oauth_signature = MakeOAuthSignature (compositeSigningKey, signature);
			
			var wc = new WebClient ();
			headers.Add ("oauth_signature", OAuth.PercentEncode (oauth_signature));
			wc.Headers [HttpRequestHeader.Authorization] = HeadersToOAuth (headers);
			
			try {
				var result = Http.ParseQueryString (wc.UploadString (new Uri (config.RequestTokenUrl), ""));

				if (result ["oauth_callback_confirmed"] != null){
					RequestToken = result ["oauth_token"];
					RequestTokenSecret = result ["oauth_token_secret"];
					
					return true;
				}
			} catch (Exception e) {
				Console.WriteLine (e);
				// fallthrough for errors
			}
			return false;
		}
		
		public bool AcquireAccessToken(string oauthVerifier) {
			try {
				var wc = new WebClient();
				
				var url = new Uri(config.AccessTokenUrl);
				
				OAuthAuthorizer.AuthorizeRequest(TwitterAccount.OAuthConfig, 
				                                  wc, 
				                                  RequestToken, 
				                                  RequestTokenSecret, 
				                                  "POST", 
				                                  url, 
				                                  "oauth_verifier=" + Uri.EscapeDataString(oauthVerifier));
				
				var res = wc.UploadString(url, "");
				
				var tokens = Http.ParseQueryString(res);
				
				ScreenName = tokens["screen_name"];
				UserId = tokens["user_id"];
				AccessToken = tokens["oauth_token"];
				AccessTokenSecret = tokens["oauth_token_secret"];			
				
				return true;
			}
			catch (Exception e) {
				Log.Error(e);
				return false;
			}
		}
		
		public static void AuthorizeRequest (OAuthConfig config, WebClient wc, string oauthToken, string oauthTokenSecret, string method, Uri uri, string data)
		{
			var headers = new Dictionary<string, string>() {
				{ "oauth_consumer_key", config.ConsumerKey },
				{ "oauth_nonce", MakeNonce () },
				{ "oauth_signature_method", "HMAC-SHA1" },
				{ "oauth_timestamp", MakeTimestamp () },
				{ "oauth_token", oauthToken },
				{ "oauth_version", "1.0" }};
			var signatureHeaders = new Dictionary<string,string> (headers);

			// Add the data and URL query string to the copy of the headers for computing the signature
			if (data != null && data != ""){
				var parsed = Http.ParseQueryString (data);
				foreach (string k in parsed.Keys){
					signatureHeaders.Add (k, OAuth.PercentEncode (parsed [k]));
					
					if (k == "oauth_verifier") {
						headers.Add(k, parsed[k]);
					}
				}
			}
			
			var nvc = Http.ParseQueryString (uri.Query);
			foreach (string key in nvc.Keys){
				if (key != null)
					signatureHeaders.Add (key, OAuth.PercentEncode (nvc [key]));
			}
			
			string signature = MakeSignature (method, uri.GetLeftPart (UriPartial.Path), signatureHeaders);
			string compositeSigningKey = MakeSigningKey (config.ConsumerSecret, oauthTokenSecret);
			string oauth_signature = MakeOAuthSignature (compositeSigningKey, signature);

			headers.Add ("oauth_signature", OAuth.PercentEncode (oauth_signature));
			
			wc.Headers [HttpRequestHeader.Authorization] = HeadersToOAuth (headers);
		}
	}
	
	public static class OAuth {
		
		// 
		// This url encoder is different than regular Url encoding found in .NET 
		// as it is used to compute the signature based on a url.   Every document
		// on the web omits this little detail leading to wasting everyone's time.
		//
		// This has got to be one of the lamest specs and requirements ever produced
		//
		public static string PercentEncode (string s)
		{
			var sb = new StringBuilder ();
			
			foreach (char c in s){
				if ((c >= 'a' && c <= 'z') || (c >= 'A' && c <= 'Z') || (c >= '0' && c <= '9') || c == '-' || c == '_' || c == '.' || c == '~')
					sb.Append (c);
				else {
					sb.AppendFormat ("%{0:X2}", (int) c);
				}
			}
			return sb.ToString ();
		}
	}
}

