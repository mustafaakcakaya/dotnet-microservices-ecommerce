namespace Shopping.Web.Pages;

public class ConfirmationModel : PageModel
{
    public string Message { get; private set; } = "Your request was completed successfully.";

    public void OnGetOrderSubmitted()
    {
        Message = "Your order was submitted successfully.";
    }

    public void OnGetContact()
    {
        Message = "Your message was sent successfully.";
    }
}
