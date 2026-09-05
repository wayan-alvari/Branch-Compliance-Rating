using BranchCompliance.Infrastructure.Identity;
using BranchCompliance.Web.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace BranchCompliance.Web.Controllers;

public sealed class AccountController(SignInManager<DemoUser> signIn) : Controller
{
    [AllowAnonymous, HttpGet]
    public IActionResult Login(string? returnUrl = null) => View(new LoginModel
    {
        ReturnUrl = Url.IsLocalUrl(returnUrl) ? returnUrl : null
    });

    [AllowAnonymous, HttpPost, EnableRateLimiting("login")]
    public async Task<IActionResult> Login(LoginModel model)
    {
        if (!ModelState.IsValid)
        {
            return View(model);
        }
        // Shared demo identities must not be globally locked by another browser.
        // A per-client rate limiter bounds authentication attempts instead.
        var result = await signIn.PasswordSignInAsync(model.Email.Trim(), model.Password,
            isPersistent: false, lockoutOnFailure: false);
        if (!result.Succeeded)
        {
            ModelState.AddModelError(string.Empty, "Sign-in failed. Check the email and password.");
            model.Password = "";
            return View(model);
        }
        return LocalRedirect(Url.IsLocalUrl(model.ReturnUrl) ? model.ReturnUrl! : "/Dashboard");
    }

    [Authorize, HttpPost]
    public async Task<IActionResult> Logout()
    {
        await signIn.SignOutAsync();
        return RedirectToAction(nameof(Login));
    }

    [AllowAnonymous, HttpGet]
    public IActionResult AccessDenied()
    {
        Response.StatusCode = StatusCodes.Status403Forbidden;
        return View();
    }
}
