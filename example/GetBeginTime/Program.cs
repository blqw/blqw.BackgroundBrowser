using System;
using System.Threading.Tasks;

namespace blqw
{
    class Program
    {
        static async Task Main(string[] args)
        {
            try
            {
                var browser = new BackgroundBrowser();
                browser.Open("http://baidu.com");
                await browser.WaitAsync();
                browser.ExecuteScript(@"
                $('#kw').val('北京时间');
                $('#su').click();");
                Console.WriteLine("click");
                await Task.Delay(1000);
                await browser.WaitAsync();
                var x = await browser.EvaluateScript("$('.op-beijingtime-date').text() + ' ' + $('.op-beijingtime-week').text() + ' '+ $('.op-beijingtime-time').text().substring(0,5) + $('.op-beijingtime-time *').text() ");
                System.Console.WriteLine(x);
            }
            finally
            {
                BackgroundBrowser.Abort();
            }
            Console.WriteLine(DateTime.Now.ToString("HH:mm:ss.fff") + " ok");
        }
    }
}
