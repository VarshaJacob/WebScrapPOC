using HtmlAgilityPack;
using IngredientBlazor.Domain;
using OpenQA.Selenium.Chrome;
using System.IO.Compression;
using System.Text;

namespace IngredientBlazor.Data
{
    public class WebScrapervice
    {

        public async Task<List<ScrapedResult>> ScrapUrlsAsync(List<string> urlResults)
        {
            var scrapedResults = new List<ScrapedResult>();

            var scrapeTasks = new List<Task>();
            foreach (var url in urlResults.Take(3))
            {
                scrapeTasks.Add(Task.Run(async () =>
                {
                    
                    var scrapedResult = await ScrapUrlHttpAsync(url);

                    //some websites, due to veriffication process/ redirects/cookies/ handling JS etc
                    //will just work from a broswer, hence trying selenium if http fails
                    if (scrapedResult.Status == ScrapedStatusEnum.Failed)
                    {
                        scrapedResult = await ScrapUrlSeleniumAsync(url);
                    }

                    lock (scrapedResults)
                    {
                        scrapedResults.Add(scrapedResult);
                    }

                }));
            }

            await Task.WhenAll(scrapeTasks);

            return scrapedResults;
        }

        public async Task<List<ScrapedResult>> ScrapUrlsOnebyOneAsync(List<string> urlResults)
        {
            var scrapedResults = new List<ScrapedResult>();

            foreach (var url in urlResults.Take(3))
            {
                var scrapedResult = await ScrapUrlHttpAsync(url);

                //some websites, due to veriffication process/ redirects/cookies/ handling JS etc
                //will just work from a broswer, hence trying selenium if http fails
                if (scrapedResult.Status == ScrapedStatusEnum.Failed)
                {
                    Console.WriteLine($"http scrap failed trying selenium: {scrapedResult.Url}");
                    scrapedResult = await ScrapUrlSeleniumAsync(url);
                }
                scrapedResults.Add(scrapedResult);
                
            }

            return scrapedResults;
        }

        public async Task<ScrapedResult> ScrapUrlAgilityAsync(string url)
        {
            // Web scraping using HtmlAgilityPack c# sdk
            // NOTE: HTTP request works just fine, hence HTML agility pack not needed.

            var scrapedResult = new ScrapedResult { Url = url, Status = ScrapedStatusEnum.InProgress };
            try
            {
                var web = new HtmlWeb();
                web.Timeout = 5000;
                var scrapedDocument = await web.LoadFromWebAsync(url);
                var content = scrapedDocument?.DocumentNode.InnerText.Trim().Replace(" +", "").Replace("\n", "") ?? string.Empty;
                scrapedResult.Content = content;
                scrapedResult.Status = ScrapedStatusEnum.Completed;
                return scrapedResult;
            }
            catch (Exception ex)
            {
                scrapedResult.Content = $"Failed: {ex.Message}";
                scrapedResult.Status = ScrapedStatusEnum.Failed;
                return scrapedResult;
            }
        }

        public async Task<ScrapedResult> ScrapUrlHttpAsync(string url)
        {
            var scrapedResult = new ScrapedResult { Url = url, Status = ScrapedStatusEnum.InProgress };
            try
            {
                var httpClient = new HttpClient();
                httpClient.DefaultRequestHeaders.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/58.0.3029.110 Safari/537.3");
                httpClient.DefaultRequestHeaders.Add("Cookie", "aitHealthCheckDone=1; AKA_A2=A; _csrf=nrp5gVMcS1J8sBB8fd3uFcxk; atrc=f30aa58e-112e-460f-9dee-c8827b34a60f; _abck=B238193A2D470419DDFB2465FFA877A9~-1~YAAQxCsRAp0W/ySRAQAAXIc4MgxGDboYNdv2i8YXxME0ZsM9rtpAd8TJulHfV8lAg1ViyZg+LdBAhCps2StL9NED/IlRq1HqlQb6Yo2CaVPysPQJQBadChMwmqSXGAj8DzApv+64aB4kvpgm0kstOERijJ/zw/87gProoR6d6OSEBB2Gl8Z/1VKDP3fDnE9DOJK7QCuzACs28YPObi1sLo+mFN8IzbOUMKAeGzWIwC47E+fRUXjj52qp+T1/M6HU5pBAmwnDB89Enk/GeEstDbLiFvqyY1tnapr5jlOKvz52LCODdADizmnUEgWdhqkf/ZsLKvGoG8E3BXKgcPtLpZvL62rfgKiVaAs6FgR+5K65sfNyuAHAvHt6A7M=~-1~-1~-1; bm_mi=02A2EA61A5D9085A311A5F6EF4859847~YAAQDKrOFw1aDCqRAQAAHsdAMhjmke5y4290UJKBSm6Y0ZtOktyMNeBuJhEQXvLFiq6RZcAd1gSd7h3f2zrZageHb2nnr9nM1auOaUqez8frRARblCo4UmCxZ2ccRtusCJcx8755llf1UDmcK1e8XOJ+/ZzHTMR8k6GBo3emhmT3vCZvanW+05R7C6xl8owAgNiFNN75tDrHci0JT5YsCHik0TiJQrLoeuEMYrqZj+E2ZZzQLjYP6J/z76k7eKQIT2dpfr1AtOu1JDxfwTZB6lpM4B6fuCoV6Epp5D9a77WCapFedh12tMUO+xUIdCDC5a4wJp8=~1; _ga=GA1.1.1861737827.1723124993; _ga_R4H2SF4K8B=GS1.1.1723124993.1.1.1723124998.0.0.0; gtm.start=1723126532072; ak_bmsc=3A9F2987BDB1379A9ACD7434485F02FF~000000000000000000000000000000~YAAQxCsRAiNmACWRAQAAwklYMhi1ApHJUU+x9a1jwF59zDPJIuld7HZRSU9Z8D0IpZYdhuYzdmsY+cpkOEVTSzx384KMaGamTHuuD43dvHBPkApQoW03OYHK8Lz6VM+V6nQvU2G+IUDVU8uHnFjSVsUPR1ijlzIzM51JO6mu0AfL4iNJ65aqAeunYaVchHaLzn4O9mHOeeAbAW3BHtuff1BjczzmWcQ5Fmx07mLyA+MFQv960eYp7a3ggJ654AGKj1ChyKKcYROG8qDVO1JKX3g1DdKW3NfvAZvEmkY+6Eiw+8oVLqZl11Nzlu4kmfzVj33S9wHxC3MV+XDZGnJV4zv50XOHT8pTKMi+/TV+ga14JVEm6z17CT1j3Al1uoaTopdiPSbZ0LYBawfvmbCtyu69NX0sUtIoFPXceJ2H1lYNnCI2aoAniyP8kJCxRc1quExe3HiHsJA3FuUIxrsAsHvwq8iYBuaZB2oYGVe9JCEzD20oZpwxgB8hVRSYVn6deirS44iBl+ax0YWpfybAsVtUwGUlNkiRwzASQhmoIzNaXYeOHIbXhbOV/J9qdBdvdFr5liaE/A8gDsmiLMQpkWoCLd3sRrorodNa; bm_sz=2A55F919CDB5137CFD0F91F9B4D0A5C8~YAAQxCsRAiVmACWRAQAAwklYMhixVPidtAbsrpuK4Yw8baVtDYqWb33uV5bg8B+fpEPrvrrj+rOS31wktdWZOCXNqrLqhd2lAdu/j36WkdIWDomm6YLVcUC/jqolqkw33zdGIr7EDPP4RUsLocBnktjcy2MO10PcGV57l6RpmLsd6nocY95BNO7fWfC0mpNAJqU4x9aSbly7FJuSVXEo8FzeGhqaGaNlaFxscXkE1D9viuWAu34nDvQzm3p3G5Ecr3kAbp2z3Sml+2Prw5SAFz3n7KhjJWwPmWzyFskTEqEfj+UOeUYB9mmfoU+zil4y4P+ImevTWW7uvnhdJ/L1tlBZtc/yWj9kJn+ueK37d74Lv/2ydYTHLH22NAT4689vbPOxt4x1WfsBTdhCPzbx/ShW5Ib+4uCjjo8Kati/8g==~4338483~3289397; bm_sv=D6F5D04E8F24E77712A4C96F91A09CDE~YAAQF4pJF4g05AGRAQAAXVBYMhhz3FoVSGMVz7v5MlJaDih+raB0K92GWSt8HIX+TJ+L8LxgHlANUg3vB8aHwXSb9tkDtYvOQIU+5vVPbwA4Sa26w+DBLVsEm/NA3K9WwBAbLG88FYC8FQnu/e7xP6yQd3+ykGeAWOulJaS219/kVSy+dSSdMAeAoPqcmu7pzYweuNmlyCWJ0k3QEnb06VInHCJqtUaENcrA18aIwrrtJ/ZlKCNyabssCjoV5COX~1");
                var response = await httpClient.GetAsync(url);
                var content = string.Empty;

                if (response.IsSuccessStatusCode)
                {
                    if (response.Content.Headers.ContentEncoding.Contains("gzip"))
                    {
                        var stream = await response.Content.ReadAsStreamAsync();
                        var gzipStream = new GZipStream(stream, CompressionMode.Decompress);
                        var reader = new StreamReader(gzipStream, Encoding.UTF8);
                        content = await reader.ReadToEndAsync();
                    }
                    else
                    {
                        content = await response.Content.ReadAsStringAsync();
                    }

                    //extract only text from html, this reduces time while uploading and indexing at OpenAI
                    var htmlDoc = new HtmlDocument();
                    htmlDoc.LoadHtml(content);
                    scrapedResult.Content = htmlDoc?.DocumentNode.InnerText.Trim().Replace(" +", "").Replace("\n", "") ?? string.Empty;

                    scrapedResult.Status = ScrapedStatusEnum.Completed;
                    return scrapedResult;
                }
                else
                {
                    scrapedResult.Content = $"Failed: {response.ReasonPhrase}";
                    scrapedResult.Status = ScrapedStatusEnum.Failed;
                }


            }
            catch (Exception ex)
            {
                scrapedResult.Content = $"Failed: {ex.Message}";
                scrapedResult.Status = ScrapedStatusEnum.Failed;
            }

            return scrapedResult;
        }
        public async Task<ScrapedResult> ScrapUrlSeleniumAsync(string url)
        {
            
            var scrapedResult = new ScrapedResult { Url = url, Status = ScrapedStatusEnum.InProgress };
            try
            {
                var content = string.Empty;
                var chromeOptions = new ChromeOptions();
                // Run Chrome in headless=new mode. Doesn't work in headless mode
                chromeOptions.AddArguments("--headless=new");
                // Disable GPU acceleration
                chromeOptions.AddArguments("--disable-gpu");
                // Disable sandbox mode
                chromeOptions.AddArguments("--no-sandbox");
                // Disable /dev/shm usage
                chromeOptions.AddArguments("--disable-dev-shm-usage");
                // Disable extensions
                chromeOptions.AddArguments("--disable-extensions"); 
                // Disable images and features that are not needed
                chromeOptions.AddUserProfilePreference("profile.default_content_setting_values.images", 2);

                using (var driver = new ChromeDriver(chromeOptions))
                {
                    driver.Navigate().GoToUrl(url);
                    content = driver.PageSource;
                    driver.Close();
                    driver.Quit();
                    
                    Console.WriteLine($"Driver is running = {driver.HasActiveDevToolsSession}");
                }

                var htmlDoc = new HtmlDocument();
                htmlDoc.LoadHtml(content);

                scrapedResult.Content = htmlDoc?.DocumentNode.InnerText.Trim().Replace(" +", "").Replace("\n", "") ?? string.Empty;

                scrapedResult.Status = ScrapedStatusEnum.Completed;
                
            }
            catch (Exception ex)
            {
                scrapedResult.Content = $"Failed: {ex.Message}";
                scrapedResult.Status = ScrapedStatusEnum.Failed;
            }

            return scrapedResult;
        }
    }
}
