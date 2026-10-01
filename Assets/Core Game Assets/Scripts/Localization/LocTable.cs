using System.Collections.Generic;

namespace Tarneeb.Localization
{
    /// <summary>
    /// The Arabic strings, keyed by their English text (so the English source text in the code is also the key, and a missing entry simply shows
    /// English). Plain logical text, no diacritics, no rich-text tags. "{0}" style placeholders are kept in both languages.
    /// Card-game terms follow the usual Levantine tarneeb vocabulary (bastoni, dinari, kabbeh, sabati).
    /// </summary>
    public static class LocTable
    {
        public static readonly Dictionary<string, string> Ar = new Dictionary<string, string>
        {
            // title and menu
            { "TARNEEB", "طرنيب" }, { "العربية", "العربية" }, { "اللغة", "اللغة" },
            { "THE CLASSIC CARD GAME", "لعبة الورق الكلاسيكية" },
            { "PLAY VS BOTS", "العب ضد الكمبيوتر" },
            { "PLAY ONLINE", "العب أونلاين" },
            { "ONLINE UNAVAILABLE", "الأونلاين غير متاح" },
            // HUD and table
            { "US", "نحن" }, { "THEM", "هم" }, { "BID", "الطلب" }, { "TRICKS", "اللمات" },
            { "Us", "نحن" }, { "Them", "هم" },
            { "Your bid", "دورك في الطلب" }, { "{0} is bidding...", "{0} يطلب..." },
            { "Choose the tarneeb suit", "اختر الطرنيب" }, { "{0} is choosing tarneeb...", "{0} يختار الطرنيب..." },
            { "Your turn", "دورك" }, { "{0} is thinking...", "{0} يفكر..." },
            { "BID {0}", "طلب {0}" }, { "PASS", "باص" },
            { "TARNEEB  {0}", "الطرنيب {0}" },
            { "{0} BIDS {1}", "{0} يطلب {1}" }, { "Clubs", "سباتي" }, { "Diamonds", "ديناري" }, { "Hearts", "كبة" }, { "Spades", "بستوني" },
            { "Everyone passed - redealing", "الكل باص - إعادة التوزيع" },
            { "The match was cancelled", "تم إلغاء المباراة" },
            { "CLUBS", "سباتي" }, { "DIAMONDS", "ديناري" }, { "HEARTS", "كبة" }, { "SPADES", "بستوني" },
            { "YOUR BID", "طلبك" }, { "CHOOSE TARNEEB", "اختر الطرنيب" },
            // round summary and game over
            { "Round won", "فزت بالجولة" }, { "Round lost", "خسرت الجولة" }, { "KABOOT!", "كبوت!" },
            { "{0} bid {1}, tarneeb {2}", "{0} طلب {1}، الطرنيب {2}" },
            { "Declarers took {0} tricks, defenders {1}", "فريق الطلب أخذ {0} لمات والخصم {1}" },
            { "NEXT ROUND", "الجولة التالية" }, { "Next round", "الجولة التالية" }, { "See result", "شاهد النتيجة" },
            { "YOU WIN!", "فزت!" }, { "YOU LOSE", "خسرت" }, { "Final score", "النتيجة النهائية" },
            { "PLAY AGAIN", "العب مجددا" }, { "MENU", "القائمة" },
            // pause and settings
            { "PAUSED", "إيقاف مؤقت" }, { "RESUME", "متابعة" }, { "LEAVE TABLE", "غادر الطاولة" },
            { "SETTINGS", "الإعدادات" }, { "SOUND EFFECTS", "المؤثرات الصوتية" }, { "MUSIC", "الموسيقى" },
            { "GAME SPEED", "سرعة اللعبة" }, { "NORMAL", "عادي" }, { "FAST", "سريع" },
            { "BOT SKILL", "مستوى الكمبيوتر" }, { "EASY", "سهل" }, { "HARD", "صعب" },
            { "LANGUAGE", "اللغة" }, { "CREDITS & LICENCES", "الشكر والتراخيص" }, { "CREDITS", "الشكر" }, { "BACK", "رجوع" },
            // chat
            { "CHAT", "الدردشة" }, { "SEND", "إرسال" }, { "Type a message...", "اكتب رسالة..." }, { "No messages yet", "لا توجد رسائل بعد" },
            { "You", "أنت" }, { "Player", "لاعب" },
            { "Good luck!", "بالتوفيق!" }, { "Nice play!", "لعبة حلوة!" }, { "Well played", "أحسنت" }, { "Oops!", "أوبس!" },
            { "Thanks!", "شكرا!" }, { "Hurry up!", "يلا بسرعة!" }, { "Nice one!", "حلوة!" }, { "Let's go!", "يلا!" }, { "Hehe", "هههه" },
            // names
            { "Ahmad", "أحمد" }, { "Omar", "عمر" }, { "Khalid", "خالد" }, { "Sara", "سارة" },
            // online
            { "QUICK MATCH", "لعب سريع" }, { "CREATE PRIVATE ROOM", "أنشئ غرفة خاصة" }, { "ROOM CODE", "رمز الغرفة" }, { "JOIN", "انضم" },
            { "START GAME", "ابدأ اللعبة" }, { "LEAVE", "غادر" },
            { "Looking for a table...", "جار البحث عن طاولة..." }, { "Creating a private room...", "جار إنشاء غرفة خاصة..." },
            { "Joining {0}...", "جار الانضمام إلى {0}..." }, { "Enter the 5-letter room code", "أدخل رمز الغرفة المكون من 5 أحرف" },
            { "Quick match", "لعب سريع" }, { "Room code: {0}", "رمز الغرفة: {0}" }, { "{0} / {1} players", "{0} / {1} لاعبين" },
            { "Waiting for the host to start...", "بانتظار المضيف ليبدأ..." },
            { "Waiting for players. Start any time, bots fill empty seats.", "بانتظار اللاعبين. ابدأ متى شئت والكمبيوتر يملأ المقاعد الفارغة." },
            { "Share the code with friends. Start any time, bots fill empty seats.", "شارك الرمز مع أصدقائك. ابدأ متى شئت والكمبيوتر يملأ المقاعد الفارغة." },
            { "Table full. Starting in {0}...", "الطاولة ممتلئة. البدء بعد {0}..." },
        };
    }
}
