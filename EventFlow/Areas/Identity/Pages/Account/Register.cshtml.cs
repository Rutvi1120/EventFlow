using System.ComponentModel.DataAnnotations;
using EventFlow.Data;
using EventFlow.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace EventFlow.Areas.Identity.Pages.Account
{
    public class RegisterModel : PageModel
    {
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly IUserStore<ApplicationUser> _userStore;
        private readonly IUserEmailStore<ApplicationUser> _emailStore;
        private readonly ApplicationDbContext _context;
        private readonly ILogger<RegisterModel> _logger;

        public RegisterModel(
            UserManager<ApplicationUser> userManager,
            IUserStore<ApplicationUser> userStore,
            ApplicationDbContext context,
            ILogger<RegisterModel> logger)
        {
            _userManager = userManager;
            _userStore = userStore;
            _emailStore = GetEmailStore();
            _context = context;
            _logger = logger;
        }

        [BindProperty]
        public InputModel Input { get; set; } = new();

        public string? ReturnUrl { get; set; }

        public List<Club> ApprovedClubs { get; set; } = new();

        public class InputModel
        {
            [Required]
            [StringLength(100)]
            [Display(Name = "Full Name")]
            public string FullName { get; set; } = string.Empty;

            [StringLength(50)]
            [Display(Name = "Student ID")]
            public string? StudentId { get; set; }

            [StringLength(100)]
            [Display(Name = "Department")]
            public string? Department { get; set; }

            [Required]
            [Display(Name = "Account Type")]
            public string AccountType { get; set; } = "Student";

            [Display(Name = "Club")]
            public int? RequestedClubId { get; set; }

            [Required]
            [EmailAddress]
            [Display(Name = "Email")]
            public string Email { get; set; } = string.Empty;

            [Required]
            [StringLength(
                100,
                ErrorMessage =
                    "The {0} must be at least {2} and at most {1} characters long.",
                MinimumLength = 8)]
            [DataType(DataType.Password)]
            [Display(Name = "Password")]
            public string Password { get; set; } = string.Empty;

            [DataType(DataType.Password)]
            [Display(Name = "Confirm Password")]
            [Compare(
                "Password",
                ErrorMessage =
                    "The password and confirmation password do not match.")]
            public string ConfirmPassword { get; set; } = string.Empty;
        }

        public async Task OnGetAsync(string? returnUrl = null)
        {
            ReturnUrl = returnUrl;
            await LoadApprovedClubs();
        }

        public async Task<IActionResult> OnPostAsync(
            string? returnUrl = null)
        {
            ReturnUrl = returnUrl;

            await LoadApprovedClubs();

            Input.AccountType = Input.AccountType.Trim();

            if (Input.AccountType != "Student" &&
                Input.AccountType != "Faculty" &&
                Input.AccountType != "ClubPresident")
            {
                ModelState.AddModelError(
                    nameof(Input.AccountType),
                    "Please select a valid account type.");
            }

            if (Input.AccountType == "Student" &&
                string.IsNullOrWhiteSpace(Input.StudentId))
            {
                ModelState.AddModelError(
                    nameof(Input.StudentId),
                    "Student ID is required for student accounts.");
            }

            if (Input.AccountType == "ClubPresident")
            {
                if (!Input.RequestedClubId.HasValue)
                {
                    ModelState.AddModelError(
                        nameof(Input.RequestedClubId),
                        "Please select a club.");
                }
                else
                {
                    var approvedClubExists =
                        await _context.Clubs.AnyAsync(c =>
                            c.Id == Input.RequestedClubId.Value &&
                            c.Status == "Approved");

                    if (!approvedClubExists)
                    {
                        ModelState.AddModelError(
                            nameof(Input.RequestedClubId),
                            "Please select a valid approved club.");
                    }
                }
            }
            else
            {
                Input.RequestedClubId = null;
            }

            if (!ModelState.IsValid)
            {
                return Page();
            }

            var email = Input.Email.Trim();

            var user = new ApplicationUser
            {
                FullName = Input.FullName.Trim(),
                Department = string.IsNullOrWhiteSpace(Input.Department)
                    ? null
                    : Input.Department.Trim(),
                StudentId = string.IsNullOrWhiteSpace(Input.StudentId)
                    ? null
                    : Input.StudentId.Trim(),
                RequestedRole = Input.AccountType,
                RequestedClubId =
                    Input.AccountType == "ClubPresident"
                        ? Input.RequestedClubId
                        : null,
                Email = email,
                UserName = email,
                IsApproved = Input.AccountType == "Student"
            };

            await _userStore.SetUserNameAsync(
                user,
                email,
                CancellationToken.None);

            await _emailStore.SetEmailAsync(
                user,
                email,
                CancellationToken.None);

            var result = await _userManager.CreateAsync(
                user,
                Input.Password);

            if (!result.Succeeded)
            {
                foreach (var error in result.Errors)
                {
                    ModelState.AddModelError(
                        string.Empty,
                        error.Description);
                }

                return Page();
            }

            _logger.LogInformation(
                "User account created with account type {AccountType}.",
                Input.AccountType);

            if (Input.AccountType == "Student")
            {
                var roleResult =
                    await _userManager.AddToRoleAsync(
                        user,
                        "Student");

                if (!roleResult.Succeeded)
                {
                    await _userManager.DeleteAsync(user);

                    foreach (var error in roleResult.Errors)
                    {
                        ModelState.AddModelError(
                            string.Empty,
                            error.Description);
                    }

                    return Page();
                }

                user.IsApproved = true;
                user.RequestedRole = "Student";

                await _userManager.UpdateAsync(user);

                TempData["RegistrationSuccess"] =
                    "Your student account has been created successfully.";

                return RedirectToPage(
                    "/Account/Login",
                    new
                    {
                        returnUrl
                    });
            }

            user.IsApproved = false;
            user.RequestedRole = Input.AccountType;

            await _userManager.UpdateAsync(user);

            var approvalMessage =
                Input.AccountType == "Faculty"
                    ? "Your Faculty account has been created and is awaiting administrator approval."
                    : "Your Club President account has been created and is awaiting administrator approval.";

            TempData["RegistrationSuccess"] = approvalMessage;

            return RedirectToPage(
                "/Account/Login",
                new
                {
                    returnUrl
                });
        }

        private async Task LoadApprovedClubs()
        {
            ApprovedClubs = await _context.Clubs
                .Where(c => c.Status == "Approved")
                .OrderBy(c => c.Name)
                .ToListAsync();
        }

        private IUserEmailStore<ApplicationUser> GetEmailStore()
        {
            if (!_userManager.SupportsUserEmail)
            {
                throw new NotSupportedException(
                    "The default UI requires a user store with email support.");
            }

            return (IUserEmailStore<ApplicationUser>)_userStore;
        }
    }
}