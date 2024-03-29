﻿using BredWeb.Data;
using BredWeb.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace BredWeb.Controllers
{
    public class GroupController : Controller
    {
        private readonly UserManager<Person> _userManager;
        private readonly SignInManager<Person> _signInManager;
        private readonly ApplicationDbContext _db;
        private readonly RoleManager<IdentityRole> _roleManager;
        private readonly IWebHostEnvironment _hostingEnvironment;

        public GroupController(UserManager<Person> userManager,
                                 SignInManager<Person> signInManager,
                                 RoleManager<IdentityRole> roleManager,
                                 ApplicationDbContext db,
                                 IWebHostEnvironment hostingEnvironment)
        {
            this._userManager = userManager;
            this._signInManager = signInManager;
            this._roleManager = roleManager;
            this._hostingEnvironment = hostingEnvironment;
            _db = db;
        }

        //GET
        public async Task<IActionResult> Index(bool popular = true)
        {
            ViewBag.nick = "";
            if (_signInManager.IsSignedIn(User))
            {
                ViewBag.nick = (await _userManager.GetUserAsync(User)).NickName;
            }

            ViewBag.Popular = popular;
            ViewBag.SearchQuery = "";
            return View(OrderGroups(_db.Groups.ToList()));
        }

        //GET
        public IActionResult Search(string searchQuery, bool popular)
        {
            ViewBag.Popular = popular;
            ViewBag.SearchQuery = searchQuery;
            List<Group>? result = new();
            if (searchQuery is not null and not "")
            {
                result = _db.Groups.Where(g => g.Title!.ToLower().Contains(searchQuery.ToLower())).ToList();
                if (!result.Any())
                {
                    TempData["Error"] = "No results found";
                    return View("Index", result);
                }
                return View("Index", OrderGroups(result, popular));
            }
            else
            {
                result = _db.Groups.ToList();
                return View("Index", OrderGroups(result, popular));
            }
        }

        private List<Group> OrderGroups(List<Group> groups, bool popular = true)
        {
            if (popular)
            {
                groups = groups.OrderBy(g => g.UserCount).ToList();
            }
            else // else = sort by newest (group creation date)
            {
                groups = groups.OrderBy(g => g.StartDate).ToList();
            }
            foreach(var group in groups)
            {
                _db.Entry(group).Collection(g => g.AdminList!).Load();
                _db.Entry(group).Collection(g => g.UserList!).Load();
            }
            return groups;
        }

        //GET
        [Authorize]
        public IActionResult Create()
        {
            return View();
        }

        //POST
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize]
        public async Task<IActionResult> Create(Group obj)
        {
            var user = await _userManager.GetUserAsync(User);

            if (ModelState.IsValid)
            {
                if (_db.Groups.Any(g => g.Title == obj.Title))
                {
                    ModelState.AddModelError("CustomError", obj.Title + " already exists.");
                }
                else
                {
                    obj.StartDate = DateTime.Now;
                    obj.Creator = user.NickName;
                    obj.UserList!.Add(user);
                    obj.AdminList!.Add(new Admin { AdminId = user.Id, Email = user.Email, UserName = user.NickName! });
                    obj.UserCount++;
                    _db.Groups.Add(obj);
                    _db.SaveChanges();
                    TempData["success"] = "Group created successfully";
                    return RedirectToAction("Index");
                }
            }
            return View(obj);
        }

        //GET
        [Authorize]
        public async Task<IActionResult> Delete(int? id)
        {
            if (id == null || id == 0)
                return NotFound();
            var groupFromDb = _db.Groups.Find(id);

            if (groupFromDb == null)
                return NotFound();

            _db.Entry(groupFromDb).Collection(g => g.AdminList!).Load();
            var user = (await _userManager.GetUserAsync(User));
            var isAdmin = await _userManager.IsInRoleAsync(user, "Admin");

            if (groupFromDb.AdminList!.Any(x => x.AdminId == user.Id) || isAdmin)
                return View(groupFromDb);
            else
                return Unauthorized();
        }

        //POST
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        [Authorize]
        public async Task<IActionResult> DeleteGroup(int? id)
        {
            var obj = _db.Groups.Find(id);
            if (obj == null)
                return NotFound();

            _db.Entry(obj).Collection(g => g.AdminList!).Load();
            var user = (await _userManager.GetUserAsync(User));
            bool isAdmin = await _userManager.IsInRoleAsync(user, "Admin");
            bool isGroupAdmin = obj.AdminList!.Any(x => x.AdminId == user.Id);

            if (isGroupAdmin || isAdmin)
            {
                IQueryable<Post> postsToDelete = _db.Posts.Where(p => p.GroupId == id);
                foreach (var post in postsToDelete)
                {
                    if(post.Type == Post.TypeEnum.Image)
                        DeleteFile(post);
                    _db.Ratings.RemoveRange(_db.Ratings.Where(r => r.RatedItemId == post.Id));
                    _db.Comments.RemoveRange(_db.Comments.Where(c => c.PostId == post.Id));
                }
                _db.Posts.RemoveRange(postsToDelete);
                _db.Admins.RemoveRange(_db.Admins.Where(a => a.GroupId == id));
                _db.Groups.Remove(obj);
                _db.SaveChanges();
                TempData["success"] = "Group deleted successfully";
                return RedirectToAction("Index");
            }
            return Unauthorized();
        }

        private void DeleteFile(Post post)
        {
            string uploadsFolder = Path.Combine(_hostingEnvironment.WebRootPath, "images");
            string filePath = Path.Combine(uploadsFolder, post.ImagePath!);

            if (System.IO.File.Exists(filePath))
            {
                try
                {
                    System.IO.File.Delete(filePath);
                }
                catch { }
            }
        }

        //GET
        [Authorize]
        public async Task<IActionResult> Join(int? id)
        {
            if (id == null || id == 0)
                return NotFound();

            var group = _db.Groups.Find(id);
            var user = await _userManager.GetUserAsync(User);

            if (group == null)
                return NotFound();

            try
            {
                group.UserList!.Add(user);
                group.UserCount++;
                _db.Groups.Update(group);
                _db.SaveChanges();
            }
            catch (Exception)
            {
                return RedirectToAction("Index");
            }
            TempData["success"] = "Group Joined successfully";
            return RedirectToAction("BrowseGroup", "Post", new { id = group.Id });
        }

        public async Task<IActionResult> Leave(int? id)
        {
            if (id == null || id == 0)
                return NotFound();

            var group = _db.Groups.Find(id);
            var user = await _userManager.GetUserAsync(User);
            if (group == null)
                return NotFound();
            _db.Entry(group).Collection(g => g.UserList!).Load();
            _db.Entry(group).Collection(g => g.AdminList!).Load();

            try
            {
                if (group.UserList!.Contains(user))
                {
                    group.UserList.Remove(user);
                    group.UserCount--;
                    _db.Groups.Update(group);
                    _db.SaveChanges();
                }
                else
                {
                    return RedirectToAction("Index");
                }
            }
            catch (Exception)
            {
                return RedirectToAction("Index");
            }
            TempData["success"] = "Group Left";
            return RedirectToAction("BrowseGroup", "Post", new { id = group.Id });
        }

        //GET
        public IActionResult Open(int? id)
        {
            if (id == null || id == 0)
                return NotFound();
            var groupFromDb = _db.Groups.Find(id);

            if (groupFromDb == null)
                return NotFound();
            return RedirectToAction("BrowseGroup", "Post", new { id = groupFromDb.Id });
        }

        //GET
        [Authorize]
        public async Task<IActionResult> Edit(int? id)
        {
            if (!_signInManager.IsSignedIn(User))
                return BadRequest();
            var user = await _userManager.GetUserAsync(User);

            if (id is null or 0)
                return NotFound();

            var group = _db.Groups.Find(id);

            if (group == null)
                return NotFound();
            _db.Entry(group).Collection(g => g.AdminList!).Load();
            if (User.IsInRole("Admin") || group.AdminList!.Any(x => x.AdminId == user.Id))
            {
                EditGroupViewModel editGroup = new()
                {
                    Id = group.Id,
                    Description = group.Description,
                    Title = group.Title,
                    StartDate = group.StartDate,
                    Creator = group.Creator,
                    UserList = group.UserList,
                    UserCount = group.UserCount,
                    Posts = group.Posts,
                    AdminList = new()
                };

                foreach (Admin a in group.AdminList!)
                {
                    editGroup.AdminList.Add(new AdminViewModel
                    {
                        Id = a.Id,
                        AdminId = a.AdminId,
                        IsSelected = false,
                        Email = a.Email,
                        UserName = a.UserName
                    });
                }

                return View(editGroup);
            }

            return Unauthorized();
        }

        //POST
        [HttpPost]
        [Authorize]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EditDescription(string Description, int Id)
        {
            Group? group = _db.Groups.Find(Id);
            if (group == null)
                return NotFound();

            _db.Entry(group).Collection(g => g.AdminList!).Load();
            var user = (await _userManager.GetUserAsync(User));
            var isAdmin = await _userManager.IsInRoleAsync(user, "Admin");

            if (group.AdminList!.Any(x => x.AdminId == user.Id) || isAdmin)
            {
                if (ModelState.IsValid)
                {
                    group.Description = Description;
                    _db.Groups.Update(group);
                    _db.SaveChanges();
                    TempData["success"] = "Success";
                    return RedirectToAction("BrowseGroup", "Post", new { id = Id });
                }

                return RedirectToAction("Index", "Group");
            }
            return Unauthorized();
        }

        //POST
        [HttpPost]
        [Authorize]
        [ActionName("Edit")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> RemoveAdmins(EditGroupViewModel obj)
        {
            Group? dbGroup = _db.Groups.Find(obj.Id);

            if (dbGroup != null)
            {
                _db.Entry(dbGroup).Collection(g => g.AdminList!).Load();
                var user = (await _userManager.GetUserAsync(User));
                var isAdmin = await _userManager.IsInRoleAsync(user, "Admin");

                if (dbGroup.AdminList!.Any(x => x.AdminId == user.Id) || isAdmin)
                {
                    if (dbGroup.AdminList!.Count <= 1)
                    {
                        TempData["Error"] = "Can't remove all admins";
                        return RedirectToAction("Edit", new { id = obj.Id });
                    }
                    foreach (var admin in obj.AdminList!)
                    {
                        if (dbGroup.AdminList.Count > 1)
                        {
                            if (admin.IsSelected)
                            {
                                _db.Admins.Remove(_db.Admins.First(a => a.AdminId == admin.AdminId));
                                dbGroup.AdminList.Remove(dbGroup.AdminList.Find(x => x.AdminId == admin.AdminId)!);
                            }
                        }
                        else
                        {
                            TempData["Error"] = "All admins were removed except the last one in the list";
                            break;
                        }
                    }

                    _db.SaveChanges();
                    return RedirectToAction("Edit", new { id = obj.Id });
                }
                return Unauthorized();
            }
            TempData["Error"] = "Group was null";
            return RedirectToAction("Edit", new { id = obj.Id });
        }

        //POST
        [HttpPost]
        [Authorize]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AddAdmin(string email, int Id)
        {
            Group? group = _db.Groups.Find(Id);

            if (ModelState.IsValid && group != null)
            {

                Person? newAdmin = _db.People.FirstOrDefault(p => p.Email == email);

                if (newAdmin is null)
                {
                    TempData["Error"] = "email not found";
                    return RedirectToAction("Edit", new { id = Id });
                }

                _db.Entry(group).Collection(g => g.AdminList!).Load();
                var user = (await _userManager.GetUserAsync(User));
                var isAdmin = await _userManager.IsInRoleAsync(user, "Admin");

                if (group.AdminList!.Any(x => x.AdminId == user.Id) || isAdmin)
                {
                    foreach (Admin admin in group.AdminList!)
                    {
                        if (admin.Email == email)
                        {
                            TempData["Error"] = "Add admin failed";
                            return RedirectToAction("Edit", new { id = Id });
                        }
                    }

                    if (newAdmin != null)
                    {
                        group.AdminList.Add(new Admin { AdminId = newAdmin.Id, Email = newAdmin.Email, UserName = newAdmin.NickName! });
                        _db.Groups.Update(group);
                        _db.SaveChanges();
                        TempData["success"] = "Success";
                        return RedirectToAction("Edit", new { id = Id });
                    }
                }
                return Unauthorized();
            }
            TempData["Error"] = "Add admin failed";
            return RedirectToAction("Edit", new { id = Id });
        }

    }
}
