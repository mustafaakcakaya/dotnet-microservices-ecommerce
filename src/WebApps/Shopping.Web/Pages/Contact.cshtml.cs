using System.ComponentModel.DataAnnotations;

namespace Shopping.Web.Pages;

public class ContactModel : PageModel
{
    [BindProperty, Required]
    public string Name { get; set; } = default!;

    [BindProperty, Required, EmailAddress]
    public string Email { get; set; } = default!;

    [BindProperty, Required]
    public string Message { get; set; } = default!;

    public IActionResult OnPost()
    {
        return ModelState.IsValid
            ? RedirectToPage("Confirmation", "Contact")
            : Page();
    }
}
