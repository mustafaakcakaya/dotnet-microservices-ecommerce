using System.ComponentModel.DataAnnotations;

namespace Shopping.Web.Pages;

public class ContactModel : PageModel
{
    [BindProperty, Required(ErrorMessage = "Ad Soyad zorunludur.")]
    public string Name { get; set; } = default!;

    [BindProperty, Required(ErrorMessage = "E-posta zorunludur."), EmailAddress(ErrorMessage = "Geçerli bir e-posta girin.")]
    public string Email { get; set; } = default!;

    [BindProperty, Required(ErrorMessage = "Mesaj zorunludur.")]
    public string Message { get; set; } = default!;

    public IActionResult OnPost()
    {
        return ModelState.IsValid
            ? RedirectToPage("Confirmation", "Contact")
            : Page();
    }
}
