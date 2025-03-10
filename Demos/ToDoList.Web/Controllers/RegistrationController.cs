using System.Security.Claims;
using Htmx;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc;
using ToDoList.Models.RequestModels.UserAccounts;
using ToDoList.Services.Interfaces;
using ToDoList.Web.Extensions;

namespace ToDoList.Web.Controllers;

public class RegistrationController : Controller
{
    private readonly IUserAccountsService _userAccountsService;

    public RegistrationController(
        IUserAccountsService userAccountsService
    )
    {
        _userAccountsService = userAccountsService;
    }

    [HttpGet]
    public IActionResult Index()
    {
        if (User.Identity?.IsAuthenticated == true)
        {
            return RedirectToAction("Index", "Home");
        }

        return View();
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Index([FromForm] RegisterNewUserAccountRequest model, CancellationToken ct)
    {
        if (ModelState.IsValid)
        {
            var serviceResult = await _userAccountsService.RegisterNewUserAccount(model, ct);

            if (serviceResult.Succeed)
            {
                var identity = new ClaimsIdentity(serviceResult.Result?.Claims, "Cookies");
                
                var authProperties = new AuthenticationProperties
                {
                    AllowRefresh = true,
                    ExpiresUtc = DateTimeOffset.Now.AddDays(7),
                    IsPersistent = true,
                };

                await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, new ClaimsPrincipal(identity), authProperties);

                Response.Htmx(h =>
                {
                    h.Redirect("/");
                });
                
                return RedirectToAction("Index", "Home");
            }
            else
            {
                ModelState.AddModelErrorsFromServiceResult(serviceResult.ProblemDetails);
            }
        }

        return Request.IsHtmx()
            ? PartialView("Forms/_RegistrationForm", model)
            : View();
    }
}