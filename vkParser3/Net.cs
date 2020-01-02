using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using System.Collections;
using System.Net;
using System.IO;
using System.Text.RegularExpressions;
using System.Drawing;

namespace vkParser3
{
    class Net
    {
        string remixsid;  //Id сессии
        public string lastCookies; //Куки

        public string GetHtml(string url, string postData) //Возвращает содержимое поданной страницы
        {
            string HTML = "";

            Regex rex1 = new Regex("remixsid=(.*?);", RegexOptions.Multiline | RegexOptions.IgnoreCase | RegexOptions.IgnorePatternWhitespace);
            if (url == "0") return "0"; //Проверка на ошибку
            HttpWebRequest myHttpWebRequest =(HttpWebRequest)HttpWebRequest.Create(url);
            //myHttpWebRequest.Proxy = new WebProxy("127.0.0.1", 8888); //В перспективе можно использовать прокси
            if (!String.IsNullOrEmpty(postData)) myHttpWebRequest.Method = "POST";
            myHttpWebRequest.Referer = "https://vk.com";
            myHttpWebRequest.UserAgent = "Mozila/14.0 (compatible; MSIE 6.0;Windows NT 5.1; SV1; MyIE2;";
            myHttpWebRequest.Accept = "image/gif, image/x-xbitmap, image/jpeg,image/pjpeg, application/x-shockwave-flash,application/vnd.ms-excel,application/vnd.ms-powerpoint,application/msword";
            myHttpWebRequest.Headers.Add("Accept-Language", "ru");
            myHttpWebRequest.ContentType = "application/x-www-form-urlencoded";
            myHttpWebRequest.KeepAlive = false;

            // передаем Сookie, полученные в предыдущем запросе
            if (!String.IsNullOrEmpty(this.remixsid))
            {
                lastCookies = "remixchk=5;remixsid=" + this.remixsid;
            }
            if (!String.IsNullOrEmpty(lastCookies))
            {
                myHttpWebRequest.Headers.Add(System.Net.HttpRequestHeader.Cookie, lastCookies);
            }
            // ставим False, чтобы при получении кода 302, не делать 
            // автоматического перенаправления
            myHttpWebRequest.AllowAutoRedirect = false;
         
            // передаем параметры
            string sQueryString = postData;
            byte[] ByteArr = System.Text.Encoding.GetEncoding(1251).GetBytes(sQueryString); //Вконтакте использует кирилическую кодировку
            try
            {
                if (!String.IsNullOrEmpty(postData))
                {
                    myHttpWebRequest.ContentLength = ByteArr.Length;
                    myHttpWebRequest.GetRequestStream().Write(ByteArr, 0, ByteArr.Length);
                };

                // делаем запрос
                HttpWebResponse myHttpWebResponse = (HttpWebResponse)myHttpWebRequest.GetResponse();
                StreamReader myStreamReader;
                
                //Сохраняем Cookie 
                lastCookies = String.IsNullOrEmpty(myHttpWebResponse.Headers["Set-Cookie"]) ? "" : myHttpWebResponse.Headers["Set-Cookie"];
                Match matc1 = rex1.Match(lastCookies);
                
                //Если есть имя сессии, то подменяем Cookie 
                if (matc1.Groups.Count == 2) { this.remixsid = matc1.Groups[1].ToString(); lastCookies = "remixchk=5;remixsid=" + this.remixsid; }
                if (myHttpWebResponse.Headers["Content-Type"].IndexOf("windows-1251") > 0)
                {
                    myStreamReader = new StreamReader(myHttpWebResponse.GetResponseStream(), Encoding.GetEncoding("windows-1251"));
                }
                else
                {
                    myStreamReader = new StreamReader(myHttpWebResponse.GetResponseStream(), Encoding.UTF8);
                }
                HTML = myStreamReader.ReadToEnd();
                if (HTML == "") //Проверяем на редирект
                {
                    HTML = this.GetHtml(myHttpWebResponse.Headers["Location"].ToString(), "");

                }
            }
            catch (Exception err)
            {
                //Ошибка в чтении страницы
                return "0";
            }
            return HTML;
        }
    }
}
