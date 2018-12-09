using blqw;
using System.Threading.Tasks;

namespace GetBeginTime
{
    class Program
    {
        static async Task Main(string[] args)
        {
            var browser = new BackgroundBrowser();
            browser.Open("http://baidu.com");
            await browser.WaitAsync();
            browser.ExecuteScript(@"
$('#kw').val('北京时间');
$('#su').click();");
            await browser.WaitAsync();
            var x = await browser.EvaluateScript("$('.op-beijingtime-date').text() + ' ' + $('.op-beijingtime-week').text() + ' '+ $('.op-beijingtime-time').text().substring(0,5) + $('.op-beijingtime-time *').text() ");
            System.Console.WriteLine(x);
        }
    }
}
