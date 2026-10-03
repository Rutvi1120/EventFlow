using EventFlow.Models;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using System.ComponentModel.DataAnnotations;

namespace EventFlow.Areas.Identity.Pages.Account
{
    public class LoginModel : PageModel
    {
        private readonly SignInManager<ApplicationUser> _signInManager;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly ILogger<LoginModel> _logger;

        public LoginModel(
            SignInManager<ApplicationUser> signInManager,
            UserManager<ApplicationUser> userManager,
            ILogger<LoginModel> logger)
        {
            _signInManager = signInManager;
            _userManager = userManager;
            _logger = logger;
        }

        [BindProperty]
        public InputModel Input { get; set; } = new();

        public IList<AuthenticationScheme> ExternalLogins { get; set; }
            = new List<AuthenticationScheme>();

        public string? ReturnUrl { get; set; }

        [TempData]
        public string? ErrorMessage { get; set; }

        public class InputModel
        {
            [Required]
            [EmailAddress]
            public string Email { get; set; } = string.Empty;

            [Required]
            [DataType(DataType.Password)]
            public string Password { get; set; } = string.Empty;

            [Display(Name = "Remember me")]
            public bool RememberMe { get; set; }
        }

        public async Task OnGetAsync(string? returnUrl = null)
        {
            if (!string.IsNullOrEmpty(ErrorMessage))
            {
                ModelState.AddModelError(
                    string.Empty,
                    ErrorMessage);
            }

            returnUrl ??= Url.Content("~/");

            await HttpContext.SignOutAsync(
                IdentityConstants.ExternalScheme);

            ExternalLogins =
                (await _signInManager
                    .GetExternalAuthenticationSchemesAsync())
                .ToList();

            ReturnUrl = returnUrl;
        }

        public async Task<IActionResult> OnPostAsync(
            string? returnUrl = null)
        {
            returnUrl ??= Url.Content("~/");

            ExternalLogins =
                (await _signInManager
                    .GetExternalAuthenticationSchemesAsync())
                .ToList();

            if (!ModelState.IsValid)
            {
                ReturnUrl = returnUrl;
                return Page();
            }

            var email = Input.Email.Trim();

            // FIND USER
            var user =
                await _userManager.FindByEmailAsync(email);

            if (user == null)
            {
                ModelState.AddModelError(
                    string.Empty,
                    "Invalid email or password.");

                ReturnUrl = returnUrl;
                return Page();
            }

            // CHECK APPROVAL
            if (!user.IsApproved)
            {
                ModelState.AddModelError(
                    string.Empty,
                    "Your account is awaiting administrator approval.");

                ReturnUrl = returnUrl;
                return Page();
            }

            // LOGIN
            var result =
                await _signInManager.PasswordSignInAsync(
                    user,
                    Input.Password,
                    Input.RememberMe,
                    lockoutOnFailure: true);

            if (result.Succeeded)
            {
                _logger.LogInformation(
                    "User {Email} logged in successfully.",
                    email);

                if (await _userManager.IsInRoleAsync(user, "Admin"))
                {
                    return RedirectToAction("Index", "Admin");
                }

                if (await _userManager.IsInRoleAsync(user, "Student"))
                {
                    return RedirectToAction("Index", "Student");
                }

                if (await _userManager.IsInRoleAsync(user, "Faculty"))
                {
                    return RedirectToAction("Index", "Faculty");
                }

                if (await _userManager.IsInRoleAsync(user, "ClubPresident"))
                {
                    return RedirectToAction("Index", "ClubPresident");
                }

                ModelState.AddModelError(
                    string.Empty,
                    "Your account does not have a valid application role.");

                return Page();
            }

            // ACCOUNT LOCKED
            if (result.IsLockedOut)
            {
                _logger.LogWarning(
                    "User account locked out.");

                return RedirectToPage("./Lockout");
            }

            // TWO FACTOR AUTHENTICATION
            if (result.RequiresTwoFactor)
            {
                return RedirectToPage(
                    "./LoginWith2fa",
                    new
                    {
                        ReturnUrl = returnUrl,
                        RememberMe = Input.RememberMe
                    });
            }

            // INVALID PASSWORD
            ModelState.AddModelError(
                string.Empty,
                "Invalid email or password.");

            ReturnUrl = returnUrl;

            return Page();
        }

        public IActionResult OnPostExternalLogin(
            string provider,
            string? returnUrl = null)
        {
            var redirectUrl =
                Url.Page(
                    "./ExternalLogin",
                    pageHandler: "Callback",
                    values: new
                    {
                        ReturnUrl = returnUrl
                    });

            var properties =
                _signInManager
                    .ConfigureExternalAuthenticationProperties(
                        provider,
                        redirectUrl);

            return new ChallengeResult(
                provider,
                properties);
        }
    }
}