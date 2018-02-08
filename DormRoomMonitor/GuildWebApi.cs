using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Net.Http;
using System.Diagnostics;
using Newtonsoft.Json;
using System.Net.Http.Headers;
using Windows.Storage;
using Windows.Storage.Streams;

namespace DormRoomMonitor
{
    public class Person
    {
        public string email { get; set; }
        public string name { get; set; }
        [JsonProperty(PropertyName = "imageUrls")]
        public List<string> imageUrls { get; set; }
        public Guid faceApiId { get; set; }
    }
    public class Attendance
    {
        public string email;
        public bool entered;
        public string location;
    }
    public class Log
    {
        public string message;
        public StorageFile image;
    }

    public class GuildWebApi
    {
        static private List<Person> getMockData()
        {
            List<Person> personas = new List<Person>();
            personas.Add(new Person
            {
                name = "shy",
                email = "shyp@wix.com",
                imageUrls = new List<string>
                    {
                        "https://scontent.fsdv3-1.fna.fbcdn.net/v/t1.0-9/23559519_10154812786021300_6692689472359614598_n.jpg?oh=6e24ca12b5609897713e005918d76037&oe=5B1FBE7F",
                        "https://scontent.fsdv3-1.fna.fbcdn.net/v/t1.0-9/23517946_10154812783481300_2218518564294336531_n.jpg?oh=fd0c1bcfdaab1751640f5c7dd1a97695&oe=5ADF35C1",
                        "https://scontent.fsdv3-1.fna.fbcdn.net/v/t1.0-9/23473169_10154812785566300_2989874843618039009_n.jpg?oh=bac7a52fcc6fdc7e1afaf635fe4a4971&oe=5ADC4937",
                        "https://scontent.fsdv3-1.fna.fbcdn.net/v/t31.0-8/16665847_1359086070781221_8536346225160018825_o.jpg?oh=f118e5a82b825d396219a24a35a0c0e5&oe=5B1ECAF4"
                    }
            });
            return personas;
        }
        static public async Task<List<Person>> getWhiteList()
        {
            if (SpeechContants.MockAPI)
            {
                return getMockData();
            }
            using (var client = new HttpClient())
            {
                try
                {
                    HttpResponseMessage response = await client.GetAsync(SpeechContants.GET_WHITE_LIST_URL);

                    response.EnsureSuccessStatusCode();

                    using (HttpContent content = response.Content)
                    {
                        string responseBody = await response.Content.ReadAsStringAsync();

                        Debug.WriteLine(responseBody.Substring(0, 50) + "........");

                        var persones = JsonConvert.DeserializeObject<List<Person>>(responseBody);

                        foreach (var persona in persones)
                        {
                            Debug.WriteLine("{0}\t{1}\t{2}", persona.email, persona.name, persona.imageUrls.Count);
                        }
                        return persones;
                    }
                }
                catch (Exception ex)
                {
                    Debug.WriteLine("exception while getting whitelist: " + ex.Message);
                    var log = new Log { message = "exception while getting whitelist: " + ex.Message };
                    GuildWebApi.updateLog(log);
                }
                return null;
            }
        }
        static public async Task updateAttendance(Attendance attendance)
        {
            using (var client = new HttpClient())
            {
                try
                {
                    client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
                    var body = JsonConvert.SerializeObject(attendance);

                    HttpResponseMessage response = await client.PostAsync(SpeechContants.POST_ATTENDANCE_URL, new StringContent(body, Encoding.UTF8, "application/json"));
                }
                catch (HttpRequestException hre)
                {
                    Debug.WriteLine("error posting: " + hre.Message);
                }
                catch (TaskCanceledException)
                {
                    Debug.WriteLine("posting canceled");
                }
                catch (Exception ex)
                {
                    Debug.WriteLine("exception while posting: " + ex.Message);
                }
                


            }
        }
        static public async Task updateLog(Log log)
        {
            using (var client = new HttpClient())
            {
                try
                {
                    client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
                    string imageToSend = await StorageFileToBase64(log.image);
                    var body = JsonConvert.SerializeObject(new { message = log.message, image = imageToSend});

                    HttpResponseMessage response = await client.PostAsync(SpeechContants.POST_LOG_URL, new StringContent(body, Encoding.UTF8, "application/json"));
                }
                catch (HttpRequestException hre)
                {
                    Debug.WriteLine("error posting: " + hre.Message);
                }
                catch (TaskCanceledException)
                {
                    Debug.WriteLine("posting canceled");
                }
                catch (Exception ex)
                {
                    Debug.WriteLine("exception while posting: " + ex.Message);
                }



            }
        }

        static private async Task<string> StorageFileToBase64(StorageFile file)
        {
            string Base64String = "";

            if (file != null)
            {
                IRandomAccessStream fileStream = await file.OpenAsync(FileAccessMode.Read);
                var reader = new DataReader(fileStream.GetInputStreamAt(0));
                await reader.LoadAsync((uint)fileStream.Size);
                byte[] byteArray = new byte[fileStream.Size];
                reader.ReadBytes(byteArray);
                Base64String = Convert.ToBase64String(byteArray);
            }

            return Base64String;
        }


    }

}
