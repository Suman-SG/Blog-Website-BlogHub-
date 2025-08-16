


using blogapp.Data;
using blogapp.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;

namespace blogapp.Controllers
{
    [Authorize(Roles = "Admin")] // Apply to entire controller
    public class AdminController : Controller
    {
        private readonly BlogDBContext _context;
        private readonly UserManager<IdentityUser> _userManager;
        private readonly SignInManager<IdentityUser> _signInManager;
        //private readonly IConfiguration _config;

        public AdminController(
            BlogDBContext context,
            UserManager<IdentityUser> userManager,
            SignInManager<IdentityUser> signInManager)
            //IConfiguration config)

        {
            _context = context;
            _userManager = userManager;
            _signInManager = signInManager;
            //_config = config;
        }

        [AllowAnonymous]
        public IActionResult Login()
        {
            return View();
        }

        [HttpPost]
        [AllowAnonymous]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Login(string username, string password)
        {
            var user = await _userManager.FindByNameAsync(username);
            if (user != null && await _userManager.IsInRoleAsync(user, "Admin"))
            {
                var result = await _signInManager.PasswordSignInAsync(user, password, false, false);
                if (result.Succeeded)
                {
                    return RedirectToAction("Dashboard");
                }
            }

            ViewBag.Message = "Invalid admin username or password.";
            return View();
        }
       
        public async Task<IActionResult> Logout()
        {
            await _signInManager.SignOutAsync();
            return RedirectToAction("Login");
        }

        public IActionResult Dashboard()
        {
            var users = _context.Users.ToList();
            var blogs = _context.BlogPosts.Include(b => b.User).ToList();

            return View((users, blogs));
        }

        [HttpPost]
        public IActionResult DeleteBlog(int id)
        {
            var post = _context.BlogPosts.FirstOrDefault(p => p.Id == id);
            if (post != null)
            {
                _context.BlogPosts.Remove(post);
                _context.SaveChanges();
            }
            return RedirectToAction("Dashboard");
        }

        [HttpPost]
        public IActionResult DeleteUser(int id)
        {
            var user = _context.Users.Include(u => u.BlogPosts).FirstOrDefault(u => u.Id == id);
            if (user != null)
            {
                _context.BlogPosts.RemoveRange(user.BlogPosts);
                _context.Users.Remove(user);
                _context.SaveChanges();
            }
            return RedirectToAction("Dashboard");
        }

        [HttpPost]
        public IActionResult ToggleAdminChoice(int id)
        {
            var post = _context.BlogPosts.FirstOrDefault(p => p.Id == id);
            if (post != null)
            {
                post.IsAdminChoice = !post.IsAdminChoice;
                _context.SaveChanges();
            }
            return RedirectToAction("Dashboard");
        }

        public IActionResult SearchUsers(string query)
        {
            var users = _context.Users
                .Where(u => u.Name.Contains(query) || u.Email.Contains(query))
                .ToList();

            var blogs = _context.BlogPosts.Include(b => b.User).ToList();
            return View("Dashboard", (users, blogs));
        }

        public IActionResult SearchBlogs(string query)
        {
            var users = _context.Users.ToList();
            var blogs = _context.BlogPosts
                .Include(b => b.User)
                .Where(b => b.Title.Contains(query) || b.Tags.Contains(query))
                .ToList();

            return View("Dashboard", (users, blogs));
        }

        public IActionResult UserDetails(int id)
        {
            var user = _context.Users
                .Include(u => u.BlogPosts)
                .ThenInclude(p => p.Comments)
                .FirstOrDefault(u => u.Id == id);

            if (user == null)
                return NotFound();

            return View(user);
        }

        public IActionResult AccessRequests()
        {
            var pending = _context.Users.Where(u => u.BlogAccessStatus == AccessStatus.Pending).ToList();
            return View(pending);
        }

        [HttpPost]
        public IActionResult ApproveRequest(int id)
        {
            var user = _context.Users.Find(id);
            if (user != null)
            {
                user.BlogAccessStatus = AccessStatus.Approved;
                _context.SaveChanges();
            }
            return RedirectToAction("AccessRequests");
        }

        [HttpPost]
        public IActionResult RejectRequest(int id)
        {
            var user = _context.Users.Find(id);
            if (user != null)
            {
                user.BlogAccessStatus = AccessStatus.Rejected;
                _context.SaveChanges();
            }
            return RedirectToAction("AccessRequests");
        }

        [HttpGet]
        public IActionResult SendNotification(bool? isForCreators)
        {
            ViewBag.IsForCreators = isForCreators ?? false;
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SendNotification(string title, string message, bool isForCreators)
        {
            try
            {
                if (string.IsNullOrEmpty(title) || string.IsNullOrEmpty(message))
                {
                    TempData["Error"] = "Title and message are required";
                    return RedirectToAction("SendNotification");
                }

                var notification = new Notification
                {
                    Title = title,
                    Message = message,
                    CreatedAt = DateTime.Now,
                    IsForCreators = isForCreators
                };

                _context.Notifications.Add(notification);
                await _context.SaveChangesAsync();

                TempData["Message"] = $"Notification sent successfully to {(isForCreators ? "creators only" : "all users")}!";
                return RedirectToAction("Dashboard");
            }
            catch (Exception ex)
            {
                TempData["Error"] = $"Error sending notification: {ex.Message}";
                return RedirectToAction("SendNotification");
            }
        }

        public IActionResult UsersList(string search)
        {
            var users = _context.Users.AsQueryable();

            if (!string.IsNullOrWhiteSpace(search))
            {
                search = search.ToLower();
                users = users.Where(u =>
                    u.Name.ToLower().Contains(search) ||
                    u.Email.ToLower().Contains(search));
            }

            return View(users.ToList());
        }

        public IActionResult BlogsList(string search)
        {
            var blogs = _context.BlogPosts
                .Include(b => b.User)
                .AsQueryable();

            if (!string.IsNullOrWhiteSpace(search))
            {
                search = search.ToLower();
                blogs = blogs.Where(b =>
                    b.Title.ToLower().Contains(search) ||
                    b.Tags.ToLower().Contains(search) ||
                    b.User.Name.ToLower().Contains(search));
            }

            return View(blogs.ToList());
        }
        [HttpGet]
        public IActionResult ChangePassword()
        {
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ChangePassword(string oldPassword, string newPassword)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null)
                return NotFound();

            var result = await _userManager.ChangePasswordAsync(user, oldPassword, newPassword);
            if (result.Succeeded)
            {
                ViewBag.Message = "Password changed successfully.";
            }
            else
            {
                ViewBag.Message = string.Join("; ", result.Errors.Select(e => e.Description));
            }
            return View();
        }


    }
}
