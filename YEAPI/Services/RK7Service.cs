using System.Net;
using System.Text;
using YEAPI.Models;

namespace YEAPI.Services
{
    public class RK7Service
    {
        // ============================================================
        // Отправка произвольного XML-запроса
        // ============================================================

        public string SendXml(
            RK7Connection connection,
            string xml)
        {
            byte[] postBytes =
                Encoding.UTF8.GetBytes(xml);

            ServicePointManager.ServerCertificateValidationCallback =
                (sender, cert, chain, sslPolicyErrors) => true;

            string uri =
                $"https://{connection.Ip}:{connection.Port}/rk7api/v0/xmlinterface.xml";

            var request =
                (HttpWebRequest)WebRequest.Create(uri);

            request.Credentials =
                new NetworkCredential(
                    connection.User,
                    connection.Password);

            request.KeepAlive = false;
            request.ProtocolVersion = HttpVersion.Version10;
            request.Method = "POST";
            request.ContentType = "application/xml";
            request.Accept = "application/xml";
            request.ContentLength = postBytes.Length;

            using (var stream = request.GetRequestStream())
            {
                stream.Write(
                    postBytes,
                    0,
                    postBytes.Length);
            }

            using var response =
                (HttpWebResponse)request.GetResponse();

            using var reader =
                new StreamReader(
                    response.GetResponseStream());

            return reader.ReadToEnd();
        }
    }
}
