using EventFlow.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.WebUtilities;
using System.ComponentModel.DataAnnotations;
using System.Text;

namespace EventFlow.Areas.Identity.Pages.Account
{
    public class ForgotPasswordModel : PageModel
    {
        private readonly UserManager<ApplicationUser> _userManager;

        public ForgotPasswordModel(
            UserManager<ApplicationUser> userManager)
        {
            _userManager = userManager;
        }

        [BindProperty]
        public InputModel Input { get; set; } = new();

        public class InputModel
        {
            [Required]
            [EmailAddress]
            public string Email { get; set; } = string.Empty;
        }

        public void OnGet()
        {
        }

        public async Task<IActionResult> OnPostAsync()
        {
            if (!ModelState.IsValid)
            {
                return Page();
            }

            var user =
                await _userManager.FindByEmailAsync(Input.Email);

            if (user == null || !user.EmailConfirmed)
            {
                return RedirectToPage("./ForgotPasswordConfirmation");
            }

            var code =
                await _userManager.GeneratePasswordResetTokenAsync(user);

            code =
                WebEncoders.Base64UrlEncode(
                    Encoding.UTF8.GetBytes(code));

            var callbackUrl =
                Url.Page(
                    "/Account/ResetPassword",
                    pageHandler: null,
                    values: new
                    {
                        area = "Identity",
                        code
                    },
                    protocol: Request.Scheme);

            return RedirectToPage(
                "./ForgotPasswordConfirmation",
                new
                {
                    email = Input.Email,
                    resetUrl = callbackUrl
                });
        }
    }
}