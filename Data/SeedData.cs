using TeamTracker.Models;
using TeamTracker.Services;
using TeamTracker.ViewModels;

namespace TeamTracker.Data;

// داده‌ی نمونه (رمز همه: 1234). تاریخچه با خود TaskService و یک ساعت دستی ساخته می‌شود
// تا همه‌چیز همان قوانین برنامه را رعایت کند.
public static class SeedData
{
    public const int Peyman = 1, Behnia = 2, Mehrdad = 3, Mojtaba = 4, Hassan = 5;

    public static DataFile Create(TimeProvider realClock)
    {
        var now = realClock.GetLocalNow().DateTime;
        var today = now.Date;

        var users = new List<AppUser>
        {
            new() { Id = Peyman,  FullName = "پیمان",   Username = "peyman",  Role = Roles.Leader, JobTitle = "تیم‌لید" },
            new() { Id = Behnia,  FullName = "بهنیا",   Username = "behnia",  Role = Roles.Member, LeaderId = Peyman, JobTitle = "عضو تیم" },
            new() { Id = Mehrdad, FullName = "مهرداد",  Username = "mehrdad", Role = Roles.Member, LeaderId = Peyman, JobTitle = "عضو تیم" },
            new() { Id = Mojtaba, FullName = "مجتبی",   Username = "mojtaba", Role = Roles.Member, LeaderId = Peyman, JobTitle = "عضو تیم" },
            new() { Id = Hassan,  FullName = "عمو حسن", Username = "hassan",  Role = Roles.Member, LeaderId = Peyman, JobTitle = "کارشناس" },
        };
        foreach (var u in users) u.PasswordHash = PasswordHasher.Hash("1234");

        var repo = new InMemoryTaskRepository(new DataFile { Users = users });
        var clock = new ManualClock(today);
        var svc = new TaskService(repo, clock);

        // زمان یک روز مشخص؛ برای امروز هیچ‌وقت از «الان» جلوتر نمی‌رود
        DateTime At(int day, double hour)
        {
            var t = today.AddDays(day).AddHours(hour);
            if (day == 0) t = new[] { t, now.AddMinutes(-5) }.Min();
            return t < today.AddDays(day) ? today.AddDays(day).AddMinutes(1) : t;
        }

        int Create(int member, int day, string title, string desc, string dos, string donts,
                   TaskPriority prio, int? deadline)
        {
            clock.Set(At(day, 9));
            return svc.CreateTasks(Peyman, new TaskFormVm
            {
                Title = title, Description = desc, Dos = dos, Donts = donts, Priority = prio,
                Deadline = deadline is null ? null : today.AddDays(deadline.Value),
                AssigneeIds = { member }
            }).Single().Id;
        }

        void Log(int member, int id, int day, double hours, string note, double at = 17)
        {
            clock.Set(At(day, at));
            svc.LogWork(member, id, new WorkLogFormVm { Date = today.AddDays(day), Hours = hours, Note = note });
        }

        void Check(int member, int id, int day, params int[] items)
        {
            clock.Set(At(day, 17.5));
            foreach (var i in items) svc.ToggleCheck(member, id, i, true);
        }

        void Submit(int member, int id, int day, string note, double at = 18)
        {
            clock.Set(At(day, at));
            svc.Submit(member, id, new CompleteTaskVm { Confirm = true, Note = note });
        }

        void Comment(int user, int id, int day, double hour, string text)
        {
            clock.Set(At(day, hour));
            svc.AddComment(user, id, text);
        }

        void Approve(int id, int day, string? note = null)
        {
            clock.Set(At(day, 10));
            svc.Approve(Peyman, id, note);
        }

        void Reject(int id, int day, string reason)
        {
            clock.Set(At(day, 10.5));
            svc.Reject(Peyman, id, reason);
        }

        var t = Create(Behnia, -36, "طراحی صفحه‌ی ورود", "فرم ورود با اعتبارسنجی سمت کلاینت و سرور.",
            "از کامپوننت‌های دیزاین سیستم استفاده شود\nپیام‌های خطا فارسی باشد\nروی موبایل تست شود",
            "رمز عبور در لاگ‌ها ذخیره نشود", TaskPriority.High, -30);
        Log(Behnia, t, -35, 4, "طراحی فرم"); Log(Behnia, t, -34, 5.5, "اعتبارسنجی"); Check(Behnia, t, -34, 0, 1);
        Log(Behnia, t, -32, 3, "تست موبایل"); Check(Behnia, t, -32, 2);
        Submit(Behnia, t, -32, "فرم کامل شد و روی موبایل هم تست شد."); Approve(t, -31, "عالی بود 👌");

        t = Create(Mehrdad, -34, "API احراز هویت", "اندپوینت‌های ورود، خروج و تمدید توکن.",
            "توکن‌ها انقضای کوتاه داشته باشند\nتست واحد نوشته شود", "رمز را بدون هش ذخیره نکن", TaskPriority.High, -27);
        Log(Mehrdad, t, -33, 6, "طراحی اندپوینت‌ها"); Log(Mehrdad, t, -32, 7, "پیاده‌سازی توکن");
        Log(Mehrdad, t, -29, 4, "تست واحد"); Check(Mehrdad, t, -29, 0, 1);
        Submit(Mehrdad, t, -28, "همه‌ی تست‌ها پاس شد."); Approve(t, -27);

        t = Create(Mojtaba, -31, "راه‌اندازی CI", "بیلد و تست خودکار روی هر Pull Request.",
            "بیلد روی هر PR اجرا شود\nنتیجه‌ی تست‌ها در PR نمایش داده شود", "", TaskPriority.Normal, -22);
        Log(Mojtaba, t, -30, 3, "پیکربندی اولیه"); Log(Mojtaba, t, -28, 4.5, "کش پکیج‌ها");
        Log(Mojtaba, t, -25, 2, "گزارش تست"); Check(Mojtaba, t, -25, 0, 1);
        Submit(Mojtaba, t, -25, "روی همه‌ی PRها فعال شد."); Approve(t, -24);

        t = Create(Hassan, -29, "بررسی نیازمندی‌های ماژول گزارش", "جمع‌آوری نیازمندی‌ها از واحدهای مختلف.",
            "با واحد مالی جلسه گذاشته شود\nخروجی در قالب سند نیازمندی باشد", "بدون هماهنگی قول زمان تحویل نده",
            TaskPriority.Normal, -20);
        Log(Hassan, t, -28, 5, "جلسه با مالی"); Log(Hassan, t, -27, 3, "جلسه با فروش"); Check(Hassan, t, -27, 0);
        Log(Hassan, t, -23, 6, "نوشتن سند"); Check(Hassan, t, -23, 1);
        Submit(Hassan, t, -23, "سند نیازمندی‌ها در درایو تیم قرار گرفت."); Approve(t, -22);

        // این یکی یک بار برگشت خورده تا تاریخچه‌ی کامل یک بازبینی دیده شود
        t = Create(Behnia, -26, "کامپوننت جدول داده", "جدول با مرتب‌سازی، صفحه‌بندی و جستجو.",
            "مرتب‌سازی ستون‌ها\nصفحه‌بندی\nجستجوی سریع", "از کتابخانه‌ی سنگین استفاده نکن", TaskPriority.Normal, -16);
        Log(Behnia, t, -25, 5, "ساختار کامپوننت"); Log(Behnia, t, -24, 6, "مرتب‌سازی"); Check(Behnia, t, -24, 0);
        Log(Behnia, t, -21, 4, "صفحه‌بندی"); Check(Behnia, t, -21, 1);
        Submit(Behnia, t, -21, "جدول آماده‌ست."); Reject(t, -20, "جستجوی سریع هنوز اضافه نشده.");
        Log(Behnia, t, -19, 3.5, "جستجوی سریع"); Check(Behnia, t, -19, 2);
        Submit(Behnia, t, -18, "جستجو هم اضافه شد."); Approve(t, -17, "حالا کامله، دمت گرم");

        t = Create(Mehrdad, -21, "بهینه‌سازی کوئری‌های داشبورد", "زمان بارگذاری داشبورد زیر یک ثانیه برسد.",
            "ایندکس‌های لازم اضافه شود\nقبل و بعد اندازه‌گیری شود", "روی دیتابیس اصلی تست نکن", TaskPriority.High, -12);
        Log(Mehrdad, t, -20, 5, "پروفایل کوئری‌ها"); Log(Mehrdad, t, -18, 6.5, "ایندکس‌ها"); Check(Mehrdad, t, -18, 0);
        Log(Mehrdad, t, -14, 3, "اندازه‌گیری نهایی"); Check(Mehrdad, t, -14, 1);
        Submit(Mehrdad, t, -13, "از ۴ ثانیه به ۰٫۷ ثانیه رسید."); Approve(t, -12, "خیلی خوب 🔥");

        t = Create(Hassan, -18, "مستندسازی فرایند تأیید", "فرایند تأیید سفارش‌ها مستند شود.",
            "نمودار فرایند\nنقش‌ها و مسئولیت‌ها", "", TaskPriority.Low, -8);
        Log(Hassan, t, -17, 4, "مصاحبه"); Log(Hassan, t, -15, 3, "نمودار"); Check(Hassan, t, -15, 0);
        Log(Hassan, t, -11, 4, "نقش‌ها"); Check(Hassan, t, -11, 1);
        Submit(Hassan, t, -10, "سند نهایی شد."); Approve(t, -9);

        t = Create(Mojtaba, -16, "رفع باگ‌های فرم ثبت‌نام", "باگ‌های گزارش‌شده در نسخه‌ی ۲٫۰.",
            "همه‌ی باگ‌های بحرانی بسته شود\nتست دستی روی سه مرورگر", "", TaskPriority.High, -9);
        Log(Mojtaba, t, -15, 6, "باگ‌های بحرانی"); Check(Mojtaba, t, -15, 0);
        Log(Mojtaba, t, -14, 5, "باگ‌های جزئی"); Log(Mojtaba, t, -11, 2.5, "تست مرورگرها"); Check(Mojtaba, t, -11, 1);
        Submit(Mojtaba, t, -10, "۱۲ باگ بسته شد."); Approve(t, -10);

        // در حال انجام و آماده‌ی تیک (برای دمو)
        t = Create(Behnia, -6, "برد کانبان با درگ‌اند‌دراپ", "برد کانبان تیم با قابلیت کشیدن کارت‌ها بین ستون‌ها.",
            "کارت‌ها بین ستون‌ها جابه‌جا شوند\nروی موبایل هم کار کند\nانیمیشن نرم داشته باشد\nحالت تاریک پشتیبانی شود",
            "از کتابخانه‌ی سنگین استفاده نکن\nکد jQuery جدید ننویس", TaskPriority.High, 2);
        Log(Behnia, t, -5, 4, "ساختار برد"); Log(Behnia, t, -4, 5, "درگ‌اند‌دراپ"); Check(Behnia, t, -4, 0);
        Log(Behnia, t, -1, 3.5, "پشتیبانی موبایل"); Check(Behnia, t, -1, 1);
        Comment(Peyman, t, -3, 12, "حالت تاریک رو هم حتماً تست کن.");
        Comment(Behnia, t, -3, 12.5, "چشم، توی چک‌لیست هست 👍");
        Log(Behnia, t, 0, 2, "انیمیشن‌ها", 11);

        Create(Behnia, -1, "صفحه‌ی پروفایل کاربر", "نمایش و ویرایش اطلاعات پروفایل.",
            "آپلود عکس پروفایل\nتغییر رمز عبور", "ایمیل را بدون تأیید عوض نکن", TaskPriority.Normal, 6);

        t = Create(Mehrdad, -4, "API گزارش ماهانه", "اندپوینت گزارش ماهانه با فیلتر تاریخ.",
            "صفحه‌بندی داشته باشد\nفیلتر بازه‌ی تاریخ شمسی\nتست واحد نوشته شود",
            "کوئری مستقیم بدون پارامتر ننویس", TaskPriority.Normal, 3);
        Log(Mehrdad, t, -3, 6, "طراحی مدل"); Check(Mehrdad, t, -3, 0);
        Log(Mehrdad, t, -1, 2.5, "کوئری‌ها"); Log(Mehrdad, t, 0, 1.5, "فیلتر تاریخ", 10);
        clock.Set(new[] { now.AddMinutes(-42), At(0, 10.5) }.Max());
        svc.TimerStart(Mehrdad, t); // تایمر روشن می‌ماند

        t = Create(Mehrdad, -5, "اعتبارسنجی فرم‌ها سمت سرور", "همه‌ی فرم‌ها سمت سرور هم اعتبارسنجی شوند.",
            "پیام خطای فارسی\nتست برای ورودی‌های نامعتبر", "", TaskPriority.High, 1);
        Log(Mehrdad, t, -5, 3, "فرم‌های ثبت‌نام"); Log(Mehrdad, t, -2, 4, "بقیه‌ی فرم‌ها"); Check(Mehrdad, t, -2, 0, 1);
        Submit(Mehrdad, t, 0, "همه‌ی فرم‌ها پوشش داده شدن و تست‌ها پاس شد.", 9.5); // منتظر تأیید پیمان

        t = Create(Mojtaba, -7, "تست رگرسیون نسخه ۲٫۱", "سناریوهای اصلی قبل از انتشار تست شوند.",
            "نتایج در شیت تست ثبت شود\nباگ‌ها با اسکرین‌شات گزارش شوند", "روی دیتابیس اصلی تست نکن",
            TaskPriority.Normal, -1);
        Log(Mojtaba, t, -6, 4, "سناریوهای ورود"); Log(Mojtaba, t, -3, 5, "سناریوهای پرداخت"); Check(Mojtaba, t, -3, 0);
        Submit(Mojtaba, t, -2, "تست‌ها انجام شد.");
        Reject(t, -1, "اسکرین‌شات باگ‌ها ضمیمه نشده؛ لطفاً اضافه کن."); // برگشت‌خورده و عقب از مهلت
        Comment(Mojtaba, t, -1, 13, "باشه، فردا صبح اسکرین‌شات‌ها رو اضافه می‌کنم.");

        Create(Mojtaba, -2, "داکرایز کردن پروژه", "Dockerfile و docker-compose برای محیط توسعه.",
            "ایمیج سبک باشد\nREADME به‌روز شود", "", TaskPriority.Low, 10);

        t = Create(Hassan, -6, "تحلیل گزارش‌های کاربران", "دسته‌بندی بازخوردهای سه ماه اخیر کاربران.",
            "دسته‌بندی موضوعی\nپنج مشکل پرتکرار", "", TaskPriority.Normal, 0);
        Log(Hassan, t, -6, 3, "جمع‌آوری"); Log(Hassan, t, -4, 4, "دسته‌بندی"); Check(Hassan, t, -4, 0);
        Log(Hassan, t, -1, 3, "جمع‌بندی"); Check(Hassan, t, -1, 1);
        Submit(Hassan, t, -1, "گزارش نهایی آماده‌ست؛ پنج مشکل اصلی مشخص شد.", 19); // منتظر تأیید

        t = Create(Hassan, -3, "جلسه‌ی نیازسنجی با واحد مالی", "نیازهای گزارش‌گیری واحد مالی.",
            "صورت‌جلسه نوشته شود\nاولویت‌ها مشخص شود", "", TaskPriority.High, 0);
        Log(Hassan, t, -2, 2, "هماهنگی جلسه"); Log(Hassan, t, 0, 1, "برگزاری جلسه", 9);

        var data = repo.Snapshot();
        // اعلان‌های قدیمی خوانده‌شده فرض می‌شوند
        foreach (var n in data.Notifications.Where(n => n.At < today.AddDays(-1))) n.IsRead = true;
        return data;
    }
}
