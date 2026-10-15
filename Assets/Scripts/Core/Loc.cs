using System;
using System.Collections.Generic;
using UnityEngine;

namespace CozyFishing.Core
{
    public enum Language { Indonesian, English }

    /// <summary>Tiny two-language string table. Language is persisted in PlayerPrefs.</summary>
    public static class Loc
    {
        const string PrefKey = "language";
        static Language? current;

        public static event Action Changed;
        const string ResetKey = "language_reset_v2";

        /// <summary>Shows another language without saving the choice (dev tests, screenshots).</summary>
        public static void Preview(Language language)
        {
            current = language;
            Changed?.Invoke();
        }

        /// <summary>Back to the player's saved language after a <see cref="Preview"/>.</summary>
        public static void EndPreview()
        {
            current = (Language)PlayerPrefs.GetInt(PrefKey, (int)Language.English);
            Changed?.Invoke();
        }

        public static Language Current
        {
            get
            {
                if (current == null)
                {
                    // One-time reset to English: an interrupted test run could have left Indonesian saved on a phone.
                    if (!PlayerPrefs.HasKey(ResetKey))
                    {
                        PlayerPrefs.SetInt(PrefKey, (int)Language.English);
                        PlayerPrefs.SetInt(ResetKey, 1);
                    }
                    current = (Language)PlayerPrefs.GetInt(PrefKey, (int)Language.English);   // English by default; switch in Settings
                }
                return current.Value;
            }
            set
            {
                current = value;
                PlayerPrefs.SetInt(PrefKey, (int)value);
                Changed?.Invoke();
            }
        }

        static readonly Dictionary<string, (string id, string en)> table = new()
        {
            ["board"] = ("Naik perahu", "Board"),
            ["leave"] = ("Turun", "Get off"),
            ["enter"] = ("Masuk", "Enter"),
            ["exit"] = ("Keluar", "Exit"),
            ["cast"] = ("Tahan · Lempar", "Hold · Cast"),
            ["release"] = ("Lepas!", "Release!"),
            ["hook"] = ("Sentak!", "Hook!"),
            ["reel"] = ("Tahan · Tarik", "Hold · Reel"),
            ["continue"] = ("Lanjut", "Continue"),
            ["tap_continue"] = ("Ketuk untuk lanjut  ›", "Tap to continue  ›"),
            ["waiting"] = ("Tunggu tarikan kuat — abaikan getaran kecil...", "Wait for a strong pull — ignore the nibbles..."),
            ["bite"] = ("Ada yang makan umpan!", "Something's biting!"),
            ["reel_hint"] = ("Tahan untuk menarik — jaga jarum di area hijau", "Hold to reel — keep the needle in the green"),
            ["row_hint"] = ("Geser joystick untuk mendayung · cari riak air", "Use the stick to row · look for ripples"),
            ["walk_hint"] = ("Geser joystick untuk berjalan", "Use the stick to walk"),
            ["creel_full"] = ("Keranjang penuh — jual ikan di Pasar dermaga", "Creel is full — sell your fish at the dock Market"),
            ["new_spot"] = ("Tempat memancing baru", "New fishing spot"),
            ["you_caught"] = ("KAMU MENDAPAT", "YOU CAUGHT"),
            ["new"] = ("BARU!", "NEW!"),
            ["snap"] = ("Tali putus!", "Snap! The line broke."),
            ["slack"] = ("Tali kendur — ikannya lepas.", "The line went slack — it escaped."),
            ["slow"] = ("Terlambat — ikannya kabur.", "Too slow — it got away."),
            ["let_go"] = ("Kamu melepaskannya.", "You let it go."),
            ["stored"] = ("{0} ikan disimpan. Keranjang kosong!", "Stored {0} fish. Creel emptied!"),
            ["morning"] = ("Pagi", "Morning"),
            ["noon"] = ("Siang", "Noon"),
            ["afternoon"] = ("Sore", "Afternoon"),
            ["evening"] = ("Petang", "Evening"),
            ["night"] = ("Malam", "Night"),
            ["open"] = ("Buka", "Open"),
            ["read"] = ("Baca jurnal", "Read journal"),
            ["sleep"] = ("Tidur", "Sleep"),
            ["market"] = ("Pasar", "Market"),
            ["map"] = ("Peta", "Map"),
            ["skip"] = ("Lewati", "Skip"),
            ["legend_caught"] = ("✦ IKAN LEGENDA ✦", "✦ LEGENDARY FISH ✦"),
            ["rod_weak"] = ("Joranmu tak kuat menahan ikan legenda ini! Beli joran yang lebih kuat di Pasar.", "Your rod can't hold this legend! Buy a stronger rod at the Market."),
            ["rarity_common"] = ("Biasa", "Common"),
            ["rarity_uncommon"] = ("Tidak biasa", "Uncommon"),
            ["rarity_rare"] = ("Langka", "Rare"),
            ["rarity_legendary"] = ("Legenda", "Legendary"),
            ["obj_0"] = ("Tangkap ikan pertamamu. Naik perahu di dermaga dan cari riak air.", "Catch your first fish. Board the boat at the dock and look for ripples."),
            ["obj_1"] = ("Bab 1 — Jurnal Kakek: \"Si Emas muncul di Kolam Air Terjun saat siang.\" Tangkap Golden Carp.", "Chapter 1 — Grandpa's journal: \"The Golden one rises in the Waterfall Pool at noon.\" Catch the Golden Carp."),
            ["obj_2"] = ("Bab 2 — Pergi ke Danau Willow. Koi Lentera hanya muncul di malam hari.", "Chapter 2 — Travel to Willow Lake. The Lantern Koi only rises at night."),
            ["obj_3"] = ("Bab 3 — Di Ngarai Pinefall hidup Sturgeon Sisik Besi. Kamu butuh joran yang kuat.", "Chapter 3 — The Ironscale Sturgeon lurks in Pinefall Gorge. You'll need a strong rod."),
            ["obj_4"] = ("Bab 4 — Di Muara Saltreed, Raja Kerapu Tua menunggu di dekat mercusuar.", "Chapter 4 — At Saltreed Estuary, the Old King Grouper waits near the lighthouse."),
            ["obj_5"] = ("Bab Terakhir — Pulang ke Riverlight. Belut Bulan muncul di malam hari.", "Final Chapter — Return to Riverlight. The Moon Eel rises at night."),
            ["obj_done"] = ("Jurnal kakek telah lengkap. Sungai ini kini milikmu — teruslah memancing!", "Grandpa's journal is complete. The river is yours now — keep fishing!"),
            ["unlocked"] = ("Lokasi baru terbuka: {0}! Pergi lewat papan Peta di dermaga.", "New location unlocked: {0}! Travel from the Map board by the dock."),
            ["objective"] = ("Tujuan:", "Goal:"),
            ["chapter_n"] = ("Bab {0}", "Chapter {0}"),
            ["day_n"] = ("Hari {0}", "Day {0}"),
            ["tab_journal"] = ("Jurnal", "Journal"),
            ["tab_bag"] = ("Tas", "Bag"),
            ["tab_map"] = ("Peta", "Map"),
            ["tab_settings"] = ("Atur", "Settings"),
            ["journal_count"] = ("Ditemukan {0}/{1}", "Found {0}/{1}"),
            ["best"] = ("Terbesar", "Best"),
            ["caught"] = ("Ditangkap", "Caught"),
            ["back"] = ("Kembali", "Back"),
            ["replay_memory"] = ("Putar kenangan", "Replay memory"),
            ["stat_caught"] = ("Ikan ditangkap", "Fish caught"),
            ["stat_species"] = ("Jenis ditemukan", "Species found"),
            ["stat_legends"] = ("Legenda", "Legends"),
            ["stat_earned"] = ("Total pendapatan", "Total earned"),
            ["stat_time"] = ("Waktu bermain", "Play time"),
            ["gear"] = ("Perlengkapan", "Gear"),
            ["equipped"] = ("Dipakai", "Equipped"),
            ["equip"] = ("Pakai", "Equip"),
            ["bait"] = ("Umpan", "Bait"),
            ["none"] = ("tidak ada", "none"),
            ["creel_contents"] = ("Keranjang {0}/{1}", "Creel {0}/{1}"),
            ["creel_empty"] = ("Keranjang kosong.", "The creel is empty."),
            ["travel"] = ("Pergi", "Travel"),
            ["you_are_here"] = ("Di sini", "You're here"),
            ["locked_until"] = ("Terbuka di Bab {0}", "Unlocks in Chapter {0}"),
            ["language"] = ("Bahasa", "Language"),
            ["music"] = ("Musik", "Music"),
            ["sfx"] = ("Efek suara", "Sound"),
            ["voice"] = ("Narasi", "Narration"),
            ["vibration"] = ("Getar", "Vibration"),
            ["on"] = ("Nyala", "On"),
            ["off"] = ("Mati", "Off"),
            ["save_quit"] = ("Simpan & ke Menu", "Save & Quit to Title"),
            ["credits_line"] = ("Riverlight — dibuat dengan Unity, Blender, Meshy, Recraft & ElevenLabs", "Riverlight — made with Unity, Blender, Meshy, Recraft & ElevenLabs"),
            ["sell_creel"] = ("Jual isi keranjang", "Sell your creel"),
            ["sell_all"] = ("Jual semua", "Sell all"),
            ["sold"] = ("Terjual! +{0} koin", "Sold! +{0} coins"),
            ["rods"] = ("Joran", "Rods"),
            ["creels"] = ("Keranjang", "Creels"),
            ["buy"] = ("Beli", "Buy"),
            ["bought"] = ("Dibeli: {0}", "Bought: {0}"),
            ["tagline"] = ("Warisan Kakek", "Grandpa's Legacy"),
            ["continue_game"] = ("Lanjutkan", "Continue"),
            ["new_game"] = ("Permainan Baru", "New Game"),
            ["settings"] = ("Pengaturan", "Settings"),
            ["begin"] = ("Mulai", "Begin"),
            ["mira_0"] = ("Selamat datang! Kamu cucu Theo, kan? Bawa ikanmu ke sini, harganya adil.", "Welcome, welcome! You're Theo's grandchild, aren't you? Bring me your fish, I pay fair coins."),
            ["mira_1"] = ("Tangkapan segar hari ini? Kakekmu dulu selalu wangi sungai dan pinus.", "Fresh catch today? Your grandpa always smelled of river water and pine."),
            ["mira_2"] = ("Butuh joran yang lebih kuat? Ikan legenda tak mau makan umpan dari ranting dedalu.", "Need a stronger rod? Legends don't bite on willow sticks, dear."),
            ["mira_3"] = ("Theo pasti bangga sekali. Nah, mau jual apa hari ini?", "Theo would be so proud of you. Now, what are you selling today?"),
            ["got_grandpa_rod"] = ("Kamu menemukan joran bambu pusaka Kakek di loteng pondok! Joran itu kini terpasang.", "You found Grandpa's heirloom bamboo rod in the cabin loft! It is now equipped."),
            ["graphics"] = ("Grafis", "Graphics"),
            ["gfx_low"] = ("Hemat", "Low"),
            ["gfx_medium"] = ("Sedang", "Medium"),
            ["gfx_high"] = ("Tinggi", "High"),
            ["gfx_hint"] = ("HP lama atau terasa patah-patah? Pilih Hemat.", "Older phone or choppy play? Choose Low."),
            ["show_fps"] = ("Tampilkan FPS", "Show FPS"),
            ["spooked"] = ("Terlalu cepat — ikannya kabur ketakutan.", "Too early — you scared the fish away."),
            ["let_it_run"] = ("Ikan menyentak! Lepaskan dulu!", "It's running! Let go of the reel!"),
            ["obj_progress"] = ("Nasihat Kakek: kenali {2} dulu — {0}/{1} jenis ikan ditemukan.", "Grandpa's advice: get to know {2} first — {0}/{1} kinds of fish found."),
            ["ready_1"] = ("Kamu sudah mengenal sungai ini. Saat siang, si Emas akan muncul di Kolam Air Terjun.", "You know this river now. At noon, the Golden one will rise in the Waterfall Pool."),
            ["ready_2"] = ("Danau ini sudah mengenal namamu. Datanglah malam hari, lentera-lentera menunggumu.", "The lake knows your name now. Come back at night, the lanterns are waiting."),
            ["ready_3"] = ("Ngarai menghargai kesabaran. Si Sisik Besi mulai bergerak di Lubang Dalam.", "The gorge respects patience. The Ironscale stirs in the Deep Hole."),
            ["ready_4"] = ("Pasang surut sudah mengenalmu. Saat senja, raja tua muncul dekat mercusuar.", "The tide knows you now. At dusk, the old king rises by the lighthouse."),
            ["ready_5"] = ("Kamu sudah hafal setiap tikungan sungai ini. Malam ini, Belut Bulan akan datang.", "You have learned every bend of this river. Tonight, the Moon Eel will come."),
            ["mira_order"] = ("Ada pesanan baru untukmu, Nak! Pelangganku sudah menunggu.", "I have new orders for you, dear! My customers are waiting."),
            ["mira_thanks"] = ("Sempurna! Persis yang diminta pelangganku. Terima kasih!", "Perfect! Just what my customers wanted. Thank you!"),
            ["orders"] = ("Pesanan Mira", "Mira's Orders"),
            ["orders_empty"] = ("Pesanan baru datang besok.", "New orders arrive tomorrow."),
            ["deliver"] = ("Antar", "Deliver"),
            ["reward"] = ("Hadiah {0}", "Reward {0}"),
            ["in_creel"] = ("di keranjang {0}/{1}", "in creel {0}/{1}"),
            ["delivered"] = ("Pesanan diantar! +{0} koin", "Order delivered! +{0} coins"),
            ["collection_reward"] = ("Koleksi {0}: {1}/{2} jenis! +{3} koin", "{0} collection: {1}/{2} species! +{3} coins"),
            ["master_angler"] = ("Semua ikan ditemukan — Pemancing Ulung! +{0} koin", "Every fish found — Master Angler! +{0} coins"),
            ["bayu_0"] = ("Ah, cucu Theo! Kakekmu dan aku memancing di batu-batu ini selama empat puluh tahun.", "Ah, Theo's grandchild! Your grandpa and I fished these rocks for forty years."),
            ["bayu_1"] = ("Pasang berbalik saat senja. Saat itulah ikan-ikan besar mulai lapar.", "The tide turns at dusk. That's when the big ones get hungry."),
            ["bayu_2"] = ("Aku masih menyalakan mercusuar setiap malam. Kebiasaan lama.", "I still keep the lighthouse burning every night. Old habits."),
            ["joko_0"] = ("Sst, pelan-pelan... ikan danau itu pemalu. Tunggu tarikan yang asli, bukan getaran kecil.", "Shh, slowly... lake fish are shy. Wait for the real pull, not the nibbles."),
            ["joko_1"] = ("Istriku bilang aku lebih cinta danau ini daripada dia. Mungkin dia benar.", "My wife says I love this lake more than her. She might be right."),
            ["joko_2"] = ("Malam hari, koi bercahaya di bawah teratai. Bawa umpan yang bagus.", "At night the koi glow under the lilies. Bring good bait."),
            ["wati_0"] = ("Selamat pagi! Pasar wangi ikan segar hari ini, berkat kamu.", "Good morning! The market smells of fresh fish today, thanks to you."),
            ["wati_1"] = ("Kakekmu dulu membawakanku ikan perch setiap Jumat. Kamu mirip sekali dengannya.", "Your grandpa used to bring me perch every Friday. You look just like him."),
            ["wati_2"] = ("Jangan pulang terlalu malam, angin senja di tepi air dingin sekali.", "Don't stay out too late, the evening breeze by the water is cold."),
            ["dimas_0"] = ("Wah, kamu punya perahu sungguhan? Bisa tangkap ikan raksasa di ngarai?", "Whoa, you have a real boat? Can you catch the giant fish in the gorge?"),
            ["dimas_1"] = ("Kalau ikannya menyentak, lepaskan sebentar! Pamanku yang mengajariku.", "When a fish runs, let it go for a moment! My uncle taught me that."),
            ["dimas_2"] = ("Suatu hari aku akan jadi pemancing terbaik di lembah ini. Setelah kamu, mungkin.", "One day I'll be the best fisher in this valley. After you, maybe."),
            ["talk"] = ("Bicara", "Talk"),
            ["sound_heading"] = ("Suara", "Sound"),
            ["choose_character"] = ("Siapa namamu?", "Who are you?"),
        };

        public static string T(string key)
        {
            if (!table.TryGetValue(key, out var v)) return key;
            return Current == Language.Indonesian ? v.id : v.en;
        }

        public static string T(string key, params object[] args) => string.Format(T(key), args);
    }
}
