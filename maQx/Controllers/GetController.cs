﻿using maQx.Models;
using maQx.Utilities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Web.Mvc;
using System.Data.Entity;
using System.Security.Claims;
using System.Linq.Expressions;
using Microsoft.AspNet.Identity;


namespace maQx.Controllers
{
    /// <summary>
    /// 
    /// </summary>
    [AjaxOnly]
    [AuthorizeGetView]
    public class GetController : Controller
    {
        /// <summary>
        /// The database
        /// </summary>
        private AppContext db = new AppContext();

        /// <summary>
        /// Organizations this instance.
        /// </summary>
        /// <returns></returns>
        [HttpGet]
        public async Task<JsonResult> Organizations()
        {
            return await Format<Organization, JOrganization>(Roles.AppAdmin, db.Organizations, "OrganizationsController", x => x.ActiveFlag);
        }

        /// <summary>
        /// Invites this instance.
        /// </summary>
        /// <returns></returns>
        [HttpGet]
        public async Task<JsonResult> Invites()
        {
            var AccessRole = User.IsInRole(Roles.AppAdmin) ? Roles.SysAdmin : Roles.AppUser;

            if (User.IsInRole(Roles.AppAdmin))
                return await Format<Invite, JInvite>(Roles.Inviter, db.Invites, "InvitesController", x => x.ActiveFlag && x.Role == AccessRole);
            else
            {
                var Organization = User.GetOrganization();
                return await Format<Invite, JInvite>(Roles.Inviter, db.Invites, "InvitesController", x => x.ActiveFlag && x.Role == AccessRole && x.Organization.Key == Organization, x => x.Organization);
            }
        }

        /// <summary>
        /// Plants this instance.
        /// </summary>
        /// <returns></returns>
        [HttpGet]
        public async Task<JsonResult> Plants()
        {
            var Organization = User.GetOrganization();
            return await Format<Plant, JPlant>(Roles.SysAdmin, db.Plants, "PlantsController", x => x.ActiveFlag && x.Organization.Key == Organization, x => x.Organization);
        }

        /// <summary>
        /// Divisions this instance.
        /// </summary>
        /// <returns></returns>
        [HttpGet]
        public async Task<JsonResult> Divisions()
        {
            var Organization = User.GetOrganization();
            return await Format<Division, JDivision>(Roles.SysAdmin, db.Divisions, "DivisionsController", x => x.ActiveFlag && x.Plant.Organization.Key == Organization, x => x.Plant.Organization);
        }

        [HttpGet]
        public async Task<JsonResult> RawMaterials()
        {
            var Division = User.GetDivision();
            return await Format<RawMaterial, JRawMaterial>(Roles.AppUser, db.RawMaterials, "RawMaterialsController", x => x.ActiveFlag && x.Division.Key == Division, x => x.Division.Plant.Organization);
        }

        [HttpGet]
        public async Task<JsonResult> Products()
        {
            var Division = User.GetDivision();
            return await Format<Product, JProduct>(Roles.AppUser, db.Products, "ProductsController", x => x.ActiveFlag && x.Division.Key == Division, x => x.Division.Plant.Organization);
        }

        /// <summary>
        /// Administrators this instance.
        /// </summary>
        /// <returns></returns>
        [HttpGet]
        public async Task<JsonResult> Administrators()
        {
            try
            {
                List<UserViewModel> Users = null;
                var Claim = ((ClaimsIdentity)User.Identity);

                if (User.IsInRole(Roles.Inviter))
                {
                    if (User.IsInRole(Roles.AppAdmin))
                        Users = await GetAdministrator(db.Administrators.Include("User").Include("Organization"), x => x.Role == Roles.SysAdmin && x.ActiveFlag, null);
                    else
                    {
                        var Key = Claim.FindFirst("Organization.Key").Value;
                        Users = await GetAdministrator(db.Administrators.Include("User").Include("Organization"), x => x.Role == Roles.AppUser && x.ActiveFlag && x.Organization.Key == Key, Claim.FindFirst("Organization.Name").Value);
                    }
                }

                if (Users == null)
                {
                    return await JsonErrorViewModel.GetUserUnauhorizedError().toJson();
                }

                return await new JsonListViewModel<UserViewModel>(Users, TableTools.GetTools(Type.GetType("maQx.Controllers.Administrators"))).toJson();
            }
            catch (Exception ex)
            {
                return Json(JsonExceptionViewModel.Get(ex), JsonRequestBehavior.AllowGet);
            }
        }

        /// <summary>
        /// Menus this instance.
        /// </summary>
        /// <returns></returns>
        [HttpGet]
        public async Task<JsonResult> Menus()
        {
            if (User.IsInRole(Roles.AppUser))
            {
                return await _UserMenus(User.Identity.GetUserId());
            }
            else
            {
                var RoleList = User.GetRoles();

                return await Format<Menus, JsonMenuViewModel, JMenus>(null, db.Menus, null, x => RoleList.Contains(x.Access), (value) =>
                {
                    return new JsonMenuViewModel()
                    {
                        Menus = value.OrderBy(x => x.Order).ToList()
                    };
                });
            }
        }

        /// <summary>
        /// Users the menus.
        /// </summary>
        /// <param name="id">The identifier.</param>
        /// <returns></returns>
        [HttpGet]
        public async Task<JsonResult> UserMenus(string id)
        {
            return await _UserMenus(id);
        }

        /// <summary>
        /// _s the user menus.
        /// </summary>
        /// <param name="id">The identifier.</param>
        /// <returns></returns>
        private async Task<JsonResult> _UserMenus(string id)
        {
            // BUG: return await Format<MenuAccess, JsonListViewModel<JMenus>, JMenuAccess>(Roles.SysAdmin, db.MenuAccess, null, x => x.User.Id == id, (value) =>
            // FIX: Update SysAdmin with AppUser role. UserMenus are access by the Users with role AppUser. 16/02/2015
            // BUG: return await Format<MenuAccess, JsonListViewModel<JMenus>, JMenuAccess>(Roles.AppUser, db.MenuAccess, null, x => x.User.Id == id, (value) =>
            // FIX: UserMenus should return JsonResult of the type of JsonMenuViewModel instead of JsonListViewModel<JMenus>. 16/02/2015
            return await Format<MenuAccess, JsonMenuViewModel, JMenuAccess>(Roles.AppUser, db.MenuAccess, null, x => x.User.Id == id, (value) =>
            {
                // BUG: return new JsonListViewModel<JMenus>()
                // FIX: return as JsonMenuViewModel instead of JsonListViewModel<JMenus>. 16/02/2015
                return new JsonMenuViewModel()
                {
                    Menus = value.Select(x => x.DepartmentMenu.Menu).OrderBy(x => x.Order).ToList()
                };
            }, x => x.User, x => x.DepartmentMenu.Menu);
        }

        /// <summary>
        /// Users the department menu.
        /// </summary>
        /// <param name="id">The identifier.</param>
        /// <returns></returns>
        [HttpGet]
        public async Task<JsonResult> UserDepartmentMenu(string id)
        {
            if (User.IsInRole(Roles.SysAdmin))
            {
                var DepartmentUser = await db.DepartmentUsers.Include(x => x.Department).Where(x => x.User.Id == id).FirstOrDefaultAsync();

                if (DepartmentUser == null)
                {
                    return await JsonErrorViewModel.GetResourceNotFoundError(Response).toJson();
                }

                var DepartmentMenu = (await db.DepartmentMenus.Include(x => x.Department.Division.Plant.Organization).Include(x => x.Menu).Where(x => x.Department.Key == DepartmentUser.Department.Key).ToListAsync()).Select(x => new JDepartmentMenu(x)).ToList();

                var DepartmentSelectedMenu = (await db.MenuAccess.Include(x => x.DepartmentMenu.Menu).Where(x => x.User.Id == id).ToListAsync()).Select(x => new JMenuAccess(x)).ToList();

                return await new JsonViewModel<Tuple<List<JDepartmentMenu>, List<JMenuAccess>>>
                {
                    Value = new Tuple<List<JDepartmentMenu>, List<JMenuAccess>>(DepartmentMenu, DepartmentSelectedMenu)
                }.toJson();
            }
            else
            {
                return await JsonErrorViewModel.GetUserUnauhorizedError().toJson();
            }
        }

        /// <summary>
        /// Departments the menu.
        /// </summary>
        /// <param name="id">The identifier.</param>
        /// <returns></returns>
        [HttpGet]
        public async Task<JsonResult> DepartmentMenu(string id)
        {
            return await _DepartmentMenu(id);
        }

        /// <summary>
        /// _s the department menu.
        /// </summary>
        /// <param name="id">The identifier.</param>
        /// <returns></returns>
        private async Task<JsonResult> _DepartmentMenu(string id)
        {
            return await Format<DepartmentMenu, JsonListViewModel<JDepartmentMenu>, JDepartmentMenu>(Roles.SysAdmin, db.DepartmentMenus, null, x => x.Department.Division.Key == id, (value) =>
            {
                return new JsonListViewModel<JDepartmentMenu>()
                {
                    List = value
                };
            }, x => x.Department.Division.Plant.Organization, x => x.Menu);
        }

        /// <summary>
        /// Departments the user.
        /// </summary>
        /// <param name="id">The identifier.</param>
        /// <returns></returns>
        [HttpGet]
        public async Task<JsonResult> DepartmentUser(string id)
        {
            return await _DepartmentUser(id);
        }

        /// <summary>
        /// _s the department user.
        /// </summary>
        /// <param name="id">The identifier.</param>
        /// <returns></returns>
        private async Task<JsonResult> _DepartmentUser(string id)
        {
            return await Format<DepartmentUser, JsonListViewModel<JDepartmentUser>, JDepartmentUser>(Roles.SysAdmin, db.DepartmentUsers, null, x => x.Department.Division.Key == id, (value) =>
            {
                return new JsonListViewModel<JDepartmentUser>()
                {
                    List = value
                };
            }, x => x.Department.Division.Plant.Organization, x => x.User);
        }

        /// <summary>
        /// Divisions the access.
        /// </summary>
        /// <param name="id">The identifier.</param>
        /// <returns></returns>
        [HttpGet]
        public async Task<JsonResult> DivisionAccess(string id)
        {
            return await _DivisionAccess(id);
        }

        /// <summary>
        /// _s the division access.
        /// </summary>
        /// <param name="id">The identifier.</param>
        /// <returns></returns>
        private async Task<JsonResult> _DivisionAccess(string id)
        {
            var Organization = User.GetOrganization();

            return await Format<AccessLevel, JsonListViewModel<JAccessLevel>, JAccessLevel>(Roles.SysAdmin, db.AccessLevels, null, x => x.User.Id == id && x.Division.Plant.Organization.Key == Organization, (value) =>
            {
                return new JsonListViewModel<JAccessLevel>()
                {
                    List = value
                };
            }, x => x.Division.Plant.Organization, x => x.User);
        }

        /// <summary>
        /// Maps the users.
        /// </summary>
        /// <returns></returns>
        [HttpGet]
        public async Task<JsonResult> MappedUsers()
        {
            var Organization = User.GetOrganization();

            return await Format<DepartmentUser, JsonListViewModel<JDepartmentUser>, JDepartmentUser>(Roles.SysAdmin, db.DepartmentUsers, null, x => x.Department.Division.Plant.Organization.Key == Organization, (value) =>
           {
               return new JsonListViewModel<JDepartmentUser>()
               {
                   List = value
               };
           }, x => x.Department.Division.Plant.Organization, x => x.User);
        }

        /// <summary>
        /// Exists the specified identifier.
        /// </summary>
        /// <param name="id">The identifier.</param>
        /// <param name="type">The type.</param>
        /// <returns></returns>
        [HttpGet]
        public async Task<JsonResult> Exists(string id, string type)
        {
            switch (type.ToLower())
            {
                case "username":
                    {
                        var Organization = Request.QueryString["ref"] != null ? await db.Organizations.FindAsync(Request.QueryString["ref"]) : null;
                        var Name = id + "@" + Organization.Domain;

                        if (Organization == null)
                        {
                            return await JsonErrorViewModel.GetResourceNotFoundError(Response).toJson();
                        }

                        var Invite = await db.Invites.SingleOrDefaultAsync(x => x.Username == Name);

                        if (Invite == null)
                        {
                            // BUG: return await List(Roles.AppAdmin, (System.Data.Entity.DbSet<ApplicationUser>)db.Users, null, x => x.UserName == Name, (value) =>
                            // FIX: User name for Invite should be access by the Role Role.Inviter. 12/12/2014
                            return await List(Roles.Inviter, (DbSet<ApplicationUser>)db.Users, null, x => x.UserName == Name, (value) =>
                            {
                                return new JsonViewModel<bool>()
                                {
                                    Value = value.Any()
                                };
                            });
                        }

                        return await new JsonViewModel<bool>() { Value = true }.toJson();
                    }
                case "department":
                    {
                        return await Format<Department, JsonListViewModel<JDepartment>, JDepartment>(Roles.SysAdmin, db.Departments, null, x => x.Division.Key == id && x.Access == Roles.SysAdmin, (value) =>
                        {
                            return new JsonListViewModel<JDepartment>()
                            {
                                List = value
                            };
                        }, x => x.Division);
                    }
                case "menus":
                    {
                        return await List(Roles.SysAdmin, db.Menus, null, x => x.Access == Roles.AppUser, (value) =>
                        {
                            return new JsonListViewModel<Menus>()
                            {
                                List = value.OrderBy(x => x.Name).ToList()
                            };
                        });
                    }
                case "department-menu":
                    {
                        return await List(Roles.SysAdmin, db.DepartmentMenus, null, x => x.Department.Division.Key == id, (value) =>
                        {
                            return new JsonListViewModel<DepartmentMenu>()
                            {
                                List = value
                            };
                        }, x => x.Department, x => x.Menu);

                    }
                case "add-department-menu":
                    {
                        var data = Request.QueryString["ref"] != null ? Newtonsoft.Json.JsonConvert.DeserializeObject<EntityManupulateHelper>(Request.QueryString["ref"]) : null;

                        if (data != null)
                        {
                            var Department = await db.Departments.FindAsync(data.Entity);

                            if (Department != null)
                            {
                                var DepartmentMenu = db.DepartmentMenus;

                                foreach (var item in data.Add)
                                {
                                    var Menu = await db.Menus.FindAsync(item);

                                    if (Menu == null)
                                    {
                                        return await JsonErrorViewModel.GetResourceNotFoundError(Response).toJson();
                                    }

                                    DepartmentMenu.Add(new DepartmentMenu
                                    {
                                        Department = Department,
                                        Menu = Menu
                                    });
                                }

                                foreach (var item in data.Remove)
                                {
                                    var Menu = await db.DepartmentMenus.FindAsync(item);

                                    if (Menu == null)
                                    {
                                        return await JsonErrorViewModel.GetResourceNotFoundError(Response).toJson();
                                    }

                                    DepartmentMenu.Remove(Menu);
                                }

                                Exception Exception = null;

                                try
                                {
                                    await db.SaveChangesAsync();
                                    return await _DepartmentMenu(id);
                                }
                                catch (Exception ex)
                                {
                                    Exception = ex;
                                }

                                return await JsonExceptionViewModel.Get(Exception).toJson();
                            }

                        }

                        return await JsonErrorViewModel.GetResourceNotFoundError(Response).toJson();
                    }
                case "add-department-user":
                    {
                        var data = Request.QueryString["ref"] != null ? Newtonsoft.Json.JsonConvert.DeserializeObject<EntityManupulateHelper>(Request.QueryString["ref"]) : null;

                        if (data != null)
                        {

                            var Department = await db.Departments.FindAsync(data.Entity);

                            if (Department != null)
                            {
                                var DepartmentUser = db.DepartmentUsers;

                                foreach (var item in data.Add)
                                {
                                    var User = await db.Users.Where(x => x.Id == item).FirstOrDefaultAsync();

                                    if (User == null)
                                    {
                                        return await JsonErrorViewModel.GetResourceNotFoundError(Response).toJson();
                                    }

                                    DepartmentUser.Add(new DepartmentUser
                                    {
                                        Department = Department,
                                        User = User
                                    });
                                }

                                foreach (var item in data.Remove)
                                {
                                    var User = await db.DepartmentUsers.FindAsync(item);

                                    if (User == null)
                                    {
                                        return await JsonErrorViewModel.GetResourceNotFoundError(Response).toJson();
                                    }

                                    DepartmentUser.Remove(User);
                                }

                                Exception Exception = null;

                                try
                                {
                                    await db.SaveChangesAsync();
                                    return await _DepartmentUser(id);
                                }
                                catch (Exception ex)
                                {
                                    Exception = ex;
                                }

                                return await JsonExceptionViewModel.Get(Exception).toJson();
                            }

                        }

                        return await JsonErrorViewModel.GetResourceNotFoundError(Response).toJson();
                    }

                case "access-division":
                    {
                        var data = Request.QueryString["ref"] != null ? Newtonsoft.Json.JsonConvert.DeserializeObject<EntityManupulateHelper>(Request.QueryString["ref"]) : null;

                        if (data != null)
                        {
                            var User = db.Users.Find(data.Entity);

                            if (User != null)
                            {
                                var AccessLevel = db.AccessLevels;

                                foreach (var item in data.Add)
                                {
                                    var Division = await db.Divisions.Where(x => x.Key == item).FirstOrDefaultAsync();

                                    if (Division == null)
                                    {
                                        return await JsonErrorViewModel.GetResourceNotFoundError(Response).toJson();
                                    }

                                    AccessLevel.Add(new AccessLevel
                                    {
                                        Division = Division,
                                        User = User
                                    });
                                }

                                foreach (var item in data.Remove)
                                {
                                    var Access = await db.AccessLevels.FindAsync(item);

                                    if (Access == null)
                                    {
                                        return await JsonErrorViewModel.GetResourceNotFoundError(Response).toJson();
                                    }

                                    AccessLevel.Remove(Access);
                                }

                                Exception Exception = null;

                                try
                                {
                                    await db.SaveChangesAsync();
                                    return await _DivisionAccess(data.Entity);
                                }
                                catch (Exception ex)
                                {
                                    Exception = ex;
                                }

                                return await JsonExceptionViewModel.Get(Exception).toJson();
                            }
                        }

                        return await JsonErrorViewModel.GetResourceNotFoundError(Response).toJson();
                    }
                case "access-menu":
                    {
                        var data = Request.QueryString["ref"] != null ? Newtonsoft.Json.JsonConvert.DeserializeObject<EntityManupulateHelper>(Request.QueryString["ref"]) : null;

                        if (data != null)
                        {
                            var User = db.Users.Find(data.Entity);
                            if (User != null)
                            {
                                var MenuAccess = db.MenuAccess;

                                foreach (var item in data.Add)
                                {
                                    var DepartmentMenu = await db.DepartmentMenus.Where(x => x.Department.Key == id && x.Menu.ID == item).FirstOrDefaultAsync();

                                    if (DepartmentMenu == null)
                                    {
                                        return await JsonErrorViewModel.GetResourceNotFoundError(Response).toJson();
                                    }

                                    MenuAccess.Add(new MenuAccess
                                    {
                                        DepartmentMenu = DepartmentMenu,
                                        User = User
                                    });
                                }

                                foreach (var item in data.Remove)
                                {
                                    var Access = await db.MenuAccess.FindAsync(item);

                                    if (Access == null)
                                    {
                                        return await JsonErrorViewModel.GetResourceNotFoundError(Response).toJson();
                                    }

                                    MenuAccess.Remove(Access);
                                }

                                Exception Exception = null;

                                try
                                {
                                    await db.SaveChangesAsync();
                                    return await UserDepartmentMenu(data.Entity);
                                }
                                catch (Exception ex)
                                {
                                    Exception = ex;
                                }

                                return await JsonExceptionViewModel.Get(Exception).toJson();
                            }
                        }

                        return await JsonErrorViewModel.GetResourceNotFoundError(Response).toJson();
                    }

                default: return await JsonErrorViewModel.GetResourceNotFoundError(Response).toJson();
            }
        }

        /// <summary>
        /// Gets the administrator.
        /// </summary>
        /// <param name="Value">The value.</param>
        /// <param name="Exp">The exp.</param>
        /// <param name="Organization">The organization.</param>
        /// <returns></returns>
        static async Task<List<UserViewModel>> GetAdministrator(IQueryable<Administrator> Value, Expression<Func<Administrator, bool>> Exp, string Organization)
        {
            try
            {
                var b = String.IsNullOrWhiteSpace(Organization);
                return await Value.Where(Exp).Select(x => new UserViewModel
                 {
                     Code = x.User.Code,
                     Name = x.User.Firstname,
                     Email = x.User.Email,
                     Organization = b ? x.Organization.Name : Organization

                 }).ToListAsync();
            }
            catch (Exception ex)
            {
                throw ex;
            }
        }

        /// <summary>
        /// Lists the specified role.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="Role">The role.</param>
        /// <param name="value">The value.</param>
        /// <param name="Controller">The controller.</param>
        /// <param name="exp">The exp.</param>
        /// <param name="Includes">The includes.</param>
        /// <returns></returns>
        private async Task<JsonResult> List<T>(string Role, DbSet<T> value, string Controller, Expression<Func<T, bool>> exp, params Expression<Func<T, object>>[] Includes)
            where T : class
        {
            return await ViewHelper.List<T, JsonViewModel>(Request, Response, Controller, Role, User, value, exp, Includes, null);
        }

        /// <summary>
        /// Lists the specified role.
        /// </summary>
        /// <typeparam name="T1">The type of the 1.</typeparam>
        /// <typeparam name="T2">The type of the 2.</typeparam>
        /// <param name="Role">The role.</param>
        /// <param name="value">The value.</param>
        /// <param name="Controller">The controller.</param>
        /// <param name="exp">The exp.</param>
        /// <param name="operation">The operation.</param>
        /// <param name="Includes">The includes.</param>
        /// <returns></returns>
        private async Task<JsonResult> List<T1, T2>(string Role, DbSet<T1> value, string Controller, Expression<Func<T1, bool>> exp, Func<List<T1>, T2> operation = null, params Expression<Func<T1, object>>[] Includes)
            where T1 : class
            where T2 : JsonViewModel
        {
            return await ViewHelper.List(Request, Response, Controller, Role, User, value, exp, Includes, operation);
        }

        /// <summary>
        /// Formats the specified role.
        /// </summary>
        /// <typeparam name="T1">The type of the 1.</typeparam>
        /// <typeparam name="T2">The type of the 2.</typeparam>
        /// <param name="Role">The role.</param>
        /// <param name="value">The value.</param>
        /// <param name="Controller">The controller.</param>
        /// <param name="exp">The exp.</param>
        /// <param name="Includes">The includes.</param>
        /// <returns></returns>
        private async Task<JsonResult> Format<T1, T2>(string Role, DbSet<T1> value, string Controller, Expression<Func<T1, bool>> exp, params Expression<Func<T1, object>>[] Includes)
            where T1 : class
            where T2 : class, IJsonBase<T1, T2>
        {
            return await ViewHelper.Format<T1, JsonViewModel, T2>(Request, Response, Controller, Role, User, value, exp, Includes, null);
        }

        /// <summary>
        /// Formats the specified role.
        /// </summary>
        /// <typeparam name="T1">The type of the 1.</typeparam>
        /// <typeparam name="T2">The type of the 2.</typeparam>
        /// <typeparam name="T3">The type of the 3.</typeparam>
        /// <param name="Role">The role.</param>
        /// <param name="value">The value.</param>
        /// <param name="Controller">The controller.</param>
        /// <param name="exp">The exp.</param>
        /// <param name="operation">The operation.</param>
        /// <param name="Includes">The includes.</param>
        /// <returns></returns>
        private async Task<JsonResult> Format<T1, T2, T3>(string Role, DbSet<T1> value, string Controller, Expression<Func<T1, bool>> exp, Func<List<T3>, T2> operation = null, params Expression<Func<T1, object>>[] Includes)
            where T1 : class
            where T2 : JsonViewModel
            where T3 : class, IJsonBase<T1, T3>
        {
            return await ViewHelper.Format<T1, T2, T3>(Request, Response, Controller, Role, User, value, exp, Includes, operation);
        }

        /// <summary>
        /// 
        /// </summary>
        public class EntityManupulateHelper
        {
            /// <summary>
            /// Gets or sets the entity.
            /// </summary>
            /// <value>
            /// The entity.
            /// </value>
            public string Entity { get; set; }
            /// <summary>
            /// Gets or sets the add.
            /// </summary>
            /// <value>
            /// The add.
            /// </value>
            public string[] Add { get; set; }
            /// <summary>
            /// Gets or sets the remove.
            /// </summary>
            /// <value>
            /// The remove.
            /// </value>
            public string[] Remove { get; set; }
        }

        /// <summary>
        /// Releases unmanaged resources and optionally releases managed resources.
        /// </summary>
        /// <param name="disposing">true to release both managed and unmanaged resources; false to release only unmanaged resources.</param>
        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                db.Dispose();
            }
            base.Dispose(disposing);
        }
    }
}