namespace Shopping.Web.Pages;

public class ConfirmationModel : PageModel
{
    public string Message { get; private set; } = "İşleminiz başarıyla tamamlandı.";

    public void OnGetOrderSubmitted()
    {
        Message = "Siparişiniz alındı.";
    }

    public void OnGetContact()
    {
        Message = "Mesajınız gönderildi.";
    }
}
