using BeeLearn.Models;

namespace BeeLearn.Data;

/// <summary>Curated starter set of 20 problems (Input, Output, Operator, Selection, Loop, Array).</summary>
public static class BankSeed
{
    public const string Starter =
        "#include <bits/stdc++.h>\nusing namespace std;\n\nint main() {\n    // tulis solusimu di sini\n    return 0;\n}\n";

    public record Spec(
        string Title, string Tags, ProblemLevel Level, string Statement,
        (string In, string Out, bool Sample)[] Tests);

    public static readonly Spec[] Problems =
    {
        // ---------- Input / Output ----------
        new("Halo Dunia", "output", ProblemLevel.Easy,
            "Cetak tulisan `Halo Dunia` (tanpa tanda kutip).",
            new[] { ("", "Halo Dunia", true), ("", "Halo Dunia", false) }),

        new("Cetak Kembali Angka", "input,output", ProblemLevel.Easy,
            "Baca satu bilangan bulat `n`, lalu cetak kembali nilai `n`.",
            new[] { ("7\n", "7", true), ("-15\n", "-15", false), ("1000000\n", "1000000", false) }),

        new("Data Diri", "input,output", ProblemLevel.Easy,
            "Baca sebuah nama (satu kata) pada baris pertama dan sebuah `umur` (bilangan bulat) pada baris kedua. " +
            "Cetak dua baris:\n\n```\nNama: <nama>\nUmur: <umur>\n```",
            new[]
            {
                ("Budi\n17\n", "Nama: Budi\nUmur: 17", true),
                ("Sari\n21\n", "Nama: Sari\nUmur: 21", false),
                ("Andi\n9\n", "Nama: Andi\nUmur: 9", false),
            }),

        // ---------- Operator ----------
        new("Penjumlahan Dua Bilangan", "operator,input,output", ProblemLevel.Easy,
            "Baca dua bilangan bulat `a` dan `b` pada satu baris. Cetak `a + b`.",
            new[]
            {
                ("2 3\n", "5", true),
                ("100 -40\n", "60", false),
                ("1000000000 1000000000\n", "2000000000", false),
            }),

        new("Lima Operasi", "operator", ProblemLevel.Easy,
            "Baca `a` dan `b` (dijamin `b` bukan 0). Cetak lima baris berturut-turut: " +
            "`a+b`, `a-b`, `a*b`, `a/b` (pembagian bulat), lalu `a%b`. Semua masukan tidak negatif.",
            new[]
            {
                ("17 5\n", "22\n12\n85\n3\n2", true),
                ("20 4\n", "24\n16\n80\n5\n0", false),
                ("7 2\n", "9\n5\n14\n3\n1", false),
            }),

        new("Luas dan Keliling Persegi Panjang", "operator", ProblemLevel.Easy,
            "Baca panjang `p` dan lebar `l`. Cetak luas (`p*l`) pada baris pertama dan keliling (`2*(p+l)`) pada baris kedua.",
            new[]
            {
                ("5 3\n", "15\n16", true),
                ("10 10\n", "100\n40", false),
                ("7 2\n", "14\n18", false),
            }),

        new("Konversi Waktu", "operator", ProblemLevel.Medium,
            "Baca total detik `t` (0 ≤ t ≤ 100000). Cetak `<jam> <menit> <detik>` pada satu baris, " +
            "dengan jam = `t/3600`, menit = `(t%3600)/60`, detik = `t%60`.",
            new[]
            {
                ("3661\n", "1 1 1", true),
                ("59\n", "0 0 59", false),
                ("7325\n", "2 2 5", false),
                ("86400\n", "24 0 0", false),
            }),

        // ---------- Selection ----------
        new("Ganjil atau Genap", "selection", ProblemLevel.Easy,
            "Baca `n`. Cetak `Genap` jika `n` habis dibagi 2, selain itu cetak `Ganjil`.",
            new[]
            {
                ("4\n", "Genap", true),
                ("7\n", "Ganjil", false),
                ("0\n", "Genap", false),
                ("-3\n", "Ganjil", false),
            }),

        new("Bilangan Terbesar dari Tiga", "selection", ProblemLevel.Easy,
            "Baca tiga bilangan bulat `a`, `b`, `c` pada satu baris. Cetak nilai terbesar.",
            new[]
            {
                ("3 9 5\n", "9", true),
                ("10 10 2\n", "10", false),
                ("-5 -2 -9\n", "-2", false),
            }),

        new("Status Kelulusan", "selection", ProblemLevel.Easy,
            "Baca `nilai` (0 ≤ nilai ≤ 100). Cetak `Lulus` jika `nilai >= 60`, selain itu `Tidak Lulus`.",
            new[]
            {
                ("75\n", "Lulus", true),
                ("60\n", "Lulus", false),
                ("59\n", "Tidak Lulus", false),
            }),

        new("Nilai Huruf", "selection", ProblemLevel.Medium,
            "Baca `nilai` (0 ≤ nilai ≤ 100). Cetak huruf mutunya:\n\n" +
            "- `A` jika `nilai >= 85`\n- `B` jika `nilai >= 70`\n- `C` jika `nilai >= 60`\n" +
            "- `D` jika `nilai >= 50`\n- `E` selain itu",
            new[]
            {
                ("88\n", "A", true),
                ("70\n", "B", false),
                ("65\n", "C", false),
                ("50\n", "D", false),
                ("40\n", "E", false),
            }),

        new("Tahun Kabisat", "selection", ProblemLevel.Medium,
            "Baca `tahun`. Cetak `Kabisat` bila (habis dibagi 4 **dan** tidak habis dibagi 100) **atau** habis dibagi 400. " +
            "Selain itu cetak `Bukan Kabisat`.",
            new[]
            {
                ("2024\n", "Kabisat", true),
                ("1900\n", "Bukan Kabisat", false),
                ("2000\n", "Kabisat", false),
                ("2023\n", "Bukan Kabisat", false),
            }),

        // ---------- Loop ----------
        new("Hitung Mundur", "loop", ProblemLevel.Easy,
            "Baca `n` (1 ≤ n ≤ 100). Cetak `n`, `n-1`, …, `1`, masing-masing pada baris tersendiri.",
            new[]
            {
                ("5\n", "5\n4\n3\n2\n1", true),
                ("1\n", "1", false),
                ("3\n", "3\n2\n1", false),
            }),

        new("Jumlah 1 sampai N", "loop", ProblemLevel.Easy,
            "Baca `n` (1 ≤ n ≤ 10000). Cetak hasil `1 + 2 + … + n`.",
            new[]
            {
                ("5\n", "15", true),
                ("1\n", "1", false),
                ("100\n", "5050", false),
                ("10000\n", "50005000", false),
            }),

        new("Faktorial", "loop", ProblemLevel.Easy,
            "Baca `n` (0 ≤ n ≤ 20). Cetak `n!` = `1 * 2 * … * n`. Definisi: `0! = 1`.",
            new[]
            {
                ("5\n", "120", true),
                ("0\n", "1", false),
                ("1\n", "1", false),
                ("10\n", "3628800", false),
                ("20\n", "2432902008176640000", false),
            }),

        new("Cek Bilangan Prima", "loop,selection", ProblemLevel.Medium,
            "Baca `n` (1 ≤ n ≤ 1000000). Cetak `Prima` jika `n` bilangan prima, selain itu `Bukan Prima`. " +
            "Catatan: 1 bukan bilangan prima.",
            new[]
            {
                ("7\n", "Prima", true),
                ("1\n", "Bukan Prima", false),
                ("2\n", "Prima", false),
                ("100\n", "Bukan Prima", false),
                ("999983\n", "Prima", false),
            }),

        new("Deret Fibonacci", "loop", ProblemLevel.Medium,
            "Baca `n` (1 ≤ n ≤ 40). Cetak `n` suku pertama deret Fibonacci dipisah spasi, dengan `F(1) = 1` dan `F(2) = 1`.",
            new[]
            {
                ("7\n", "1 1 2 3 5 8 13", true),
                ("1\n", "1", false),
                ("2\n", "1 1", false),
                ("10\n", "1 1 2 3 5 8 13 21 34 55", false),
            }),

        // ---------- Array ----------
        new("Jumlah Elemen Array", "array,loop", ProblemLevel.Easy,
            "Baris pertama berisi `n` (1 ≤ n ≤ 1000). Baris kedua berisi `n` bilangan bulat dipisah spasi. " +
            "Cetak jumlah seluruh elemen.",
            new[]
            {
                ("4\n1 2 3 4\n", "10", true),
                ("1\n-5\n", "-5", false),
                ("5\n10 20 30 40 50\n", "150", false),
            }),

        new("Nilai Maksimum Array", "array", ProblemLevel.Easy,
            "Baris pertama berisi `n` (1 ≤ n ≤ 1000). Baris kedua berisi `n` bilangan bulat. Cetak elemen terbesar.",
            new[]
            {
                ("4\n3 9 1 7\n", "9", true),
                ("1\n42\n", "42", false),
                ("5\n-1 -9 -3 -2 -8\n", "-1", false),
            }),

        new("Hitung Bilangan Genap dalam Array", "array,selection", ProblemLevel.Easy,
            "Baris pertama berisi `n` (1 ≤ n ≤ 1000). Baris kedua berisi `n` bilangan bulat. " +
            "Cetak banyaknya elemen yang genap.",
            new[]
            {
                ("5\n1 2 3 4 6\n", "3", true),
                ("3\n1 3 5\n", "0", false),
                ("4\n2 4 6 8\n", "4", false),
            }),

        // ================= VARIAN TAMBAHAN =================

        // ---------- Input / Output ----------
        new("Cetak Tiga Baris", "output", ProblemLevel.Easy,
            "Cetak tiga baris berikut persis:\n\n```\nBaris Pertama\nBaris Kedua\nBaris Ketiga\n```",
            new[]
            {
                ("", "Baris Pertama\nBaris Kedua\nBaris Ketiga", true),
                ("", "Baris Pertama\nBaris Kedua\nBaris Ketiga", false),
            }),

        new("Ulangi Kata", "input,output,loop", ProblemLevel.Easy,
            "Baca sebuah kata `s` pada baris pertama dan bilangan `n` pada baris kedua. " +
            "Cetak `s` sebanyak `n` kali, masing-masing pada baris tersendiri.",
            new[]
            {
                ("halo\n3\n", "halo\nhalo\nhalo", true),
                ("ok\n1\n", "ok", false),
                ("bee\n2\n", "bee\nbee", false),
            }),

        new("Identitas Lengkap", "input,output", ProblemLevel.Easy,
            "Baca `nama` (satu kata), `kota` (satu kata), dan `umur` (bilangan bulat) — semuanya dipisah spasi/baris. " +
            "Cetak: `<nama> dari <kota>, umur <umur> tahun`",
            new[]
            {
                ("Budi Jakarta 17\n", "Budi dari Jakarta, umur 17 tahun", true),
                ("Sari Bandung 20\n", "Sari dari Bandung, umur 20 tahun", false),
                ("Andi Medan 9\n", "Andi dari Medan, umur 9 tahun", false),
            }),

        // ---------- Operator ----------
        new("Selisih Kuadrat", "operator", ProblemLevel.Easy,
            "Baca `a` dan `b`. Cetak nilai `a*a - b*b`.",
            new[]
            {
                ("5 3\n", "16", true),
                ("10 10\n", "0", false),
                ("7 2\n", "45", false),
            }),

        new("Celsius ke Fahrenheit", "operator", ProblemLevel.Easy,
            "Baca suhu Celsius `c` (bilangan bulat). Cetak suhu Fahrenheit = `c*9/5 + 32` " +
            "(gunakan aritmetika bilangan bulat).",
            new[]
            {
                ("100\n", "212", true),
                ("0\n", "32", false),
                ("37\n", "98", false),
                ("-40\n", "-40", false),
            }),

        new("Total Harga dengan Pajak", "operator", ProblemLevel.Easy,
            "Baca harga satuan `h` dan jumlah `q`. Subtotal = `h*q`, pajak = `subtotal/10` (pembagian bulat). " +
            "Cetak `subtotal + pajak`.",
            new[]
            {
                ("1000 3\n", "3300", true),
                ("500 2\n", "1100", false),
                ("100 10\n", "1100", false),
            }),

        new("Digit Satuan dan Puluhan", "operator", ProblemLevel.Easy,
            "Baca bilangan bulat non-negatif `n`. Cetak digit satuan (`n%10`) lalu digit puluhan (`n/10%10`), " +
            "dipisah spasi.",
            new[]
            {
                ("847\n", "7 4", true),
                ("5\n", "5 0", false),
                ("90\n", "0 9", false),
            }),

        // ---------- Selection ----------
        new("Tanda Bilangan", "selection", ProblemLevel.Easy,
            "Baca `n`. Cetak `Positif` jika `n > 0`, `Negatif` jika `n < 0`, `Nol` jika `n = 0`.",
            new[]
            {
                ("5\n", "Positif", true),
                ("-3\n", "Negatif", false),
                ("0\n", "Nol", false),
            }),

        new("Maksimum dari Empat", "selection", ProblemLevel.Easy,
            "Baca empat bilangan bulat pada satu baris. Cetak nilai terbesar.",
            new[]
            {
                ("3 9 5 7\n", "9", true),
                ("1 1 1 1\n", "1", false),
                ("-5 -2 -9 -1\n", "-1", false),
            }),

        new("Bisakah Membentuk Segitiga", "selection", ProblemLevel.Medium,
            "Baca tiga panjang sisi `a`, `b`, `c` (positif). Cetak `Ya` bila ketiganya dapat membentuk segitiga " +
            "(jumlah dua sisi mana pun lebih besar dari sisi ketiga), selain itu `Tidak`.",
            new[]
            {
                ("3 4 5\n", "Ya", true),
                ("1 2 3\n", "Tidak", false),
                ("5 5 5\n", "Ya", false),
                ("10 1 1\n", "Tidak", false),
            }),

        new("Jam Operasional", "selection", ProblemLevel.Easy,
            "Baca `jam` (0 ≤ jam ≤ 23). Cetak `Buka` jika `8 <= jam < 21`, selain itu `Tutup`.",
            new[]
            {
                ("10\n", "Buka", true),
                ("8\n", "Buka", false),
                ("21\n", "Tutup", false),
                ("3\n", "Tutup", false),
            }),

        new("Nama Hari", "selection", ProblemLevel.Easy,
            "Baca `n` (1 ≤ n ≤ 7). Cetak nama hari: 1=Senin, 2=Selasa, 3=Rabu, 4=Kamis, 5=Jumat, 6=Sabtu, 7=Minggu.",
            new[]
            {
                ("1\n", "Senin", true),
                ("5\n", "Jumat", false),
                ("7\n", "Minggu", false),
            }),

        // ---------- Loop ----------
        new("Tabel Perkalian", "loop", ProblemLevel.Easy,
            "Baca `n`. Cetak sepuluh baris dari `n x 1 = <hasil>` sampai `n x 10 = <hasil>`.",
            new[]
            {
                ("2\n",
                 "2 x 1 = 2\n2 x 2 = 4\n2 x 3 = 6\n2 x 4 = 8\n2 x 5 = 10\n2 x 6 = 12\n2 x 7 = 14\n2 x 8 = 16\n2 x 9 = 18\n2 x 10 = 20",
                 true),
                ("5\n",
                 "5 x 1 = 5\n5 x 2 = 10\n5 x 3 = 15\n5 x 4 = 20\n5 x 5 = 25\n5 x 6 = 30\n5 x 7 = 35\n5 x 8 = 40\n5 x 9 = 45\n5 x 10 = 50",
                 false),
            }),

        new("Deret Genap", "loop", ProblemLevel.Easy,
            "Baca `n` (n ≥ 2). Cetak semua bilangan genap dari 2 sampai `n` (inklusif) dipisah spasi.",
            new[]
            {
                ("10\n", "2 4 6 8 10", true),
                ("2\n", "2", false),
                ("7\n", "2 4 6", false),
            }),

        new("Jumlah Bilangan Ganjil", "loop", ProblemLevel.Easy,
            "Baca `n`. Cetak jumlah semua bilangan ganjil dari 1 sampai `n`.",
            new[]
            {
                ("5\n", "9", true),
                ("1\n", "1", false),
                ("10\n", "25", false),
                ("9\n", "25", false),
            }),

        new("Perpangkatan", "loop", ProblemLevel.Easy,
            "Baca `basis` dan `pangkat` (0 ≤ pangkat ≤ 30, hasil dijamin muat di bilangan 64-bit). " +
            "Cetak `basis` pangkat `pangkat`.",
            new[]
            {
                ("2 10\n", "1024", true),
                ("5 0\n", "1", false),
                ("3 4\n", "81", false),
                ("2 30\n", "1073741824", false),
            }),

        new("Jumlah Digit", "loop", ProblemLevel.Easy,
            "Baca bilangan bulat non-negatif `n`. Cetak jumlah dari digit-digitnya.",
            new[]
            {
                ("12345\n", "15", true),
                ("0\n", "0", false),
                ("999\n", "27", false),
                ("100000\n", "1", false),
            }),

        new("Balik Bilangan", "loop", ProblemLevel.Medium,
            "Baca bilangan bulat positif `n`. Cetak `n` dengan urutan digit dibalik " +
            "(nol di depan otomatis hilang, mis. `120` → `21`).",
            new[]
            {
                ("123\n", "321", true),
                ("5\n", "5", false),
                ("120\n", "21", false),
                ("1000\n", "1", false),
            }),

        new("FPB Dua Bilangan", "loop", ProblemLevel.Medium,
            "Baca dua bilangan bulat positif `a` dan `b`. Cetak FPB (faktor persekutuan terbesar) keduanya.",
            new[]
            {
                ("12 18\n", "6", true),
                ("7 13\n", "1", false),
                ("100 25\n", "25", false),
                ("48 36\n", "12", false),
            }),

        // ---------- Array ----------
        new("Nilai Minimum Array", "array", ProblemLevel.Easy,
            "Baris pertama berisi `n` (1 ≤ n ≤ 1000). Baris kedua berisi `n` bilangan bulat. Cetak elemen terkecil.",
            new[]
            {
                ("4\n3 9 1 7\n", "1", true),
                ("1\n42\n", "42", false),
                ("5\n5 4 3 2 8\n", "2", false),
            }),

        new("Rata-Rata Array", "array,loop", ProblemLevel.Easy,
            "Baris pertama berisi `n`. Baris kedua berisi `n` bilangan bulat. " +
            "Cetak rata-rata sebagai pembagian bulat (`jumlah / n`).",
            new[]
            {
                ("4\n10 20 30 40\n", "25", true),
                ("3\n1 2 3\n", "2", false),
                ("5\n10 10 10 10 15\n", "11", false),
            }),

        new("Cari Elemen dalam Array", "array,selection", ProblemLevel.Medium,
            "Baris pertama berisi `n` dan `x`. Baris kedua berisi `n` bilangan bulat. " +
            "Cetak posisi (indeks berbasis 1) kemunculan pertama `x`, atau `-1` bila tidak ada.",
            new[]
            {
                ("5 7\n1 3 7 7 9\n", "3", true),
                ("4 10\n1 2 3 4\n", "-1", false),
                ("3 1\n1 1 1\n", "1", false),
            }),

        new("Hitung Kemunculan Elemen", "array,loop", ProblemLevel.Easy,
            "Baris pertama berisi `n` dan `x`. Baris kedua berisi `n` bilangan bulat. Cetak berapa kali `x` muncul.",
            new[]
            {
                ("5 7\n7 1 7 3 7\n", "3", true),
                ("4 0\n1 2 3 4\n", "0", false),
                ("3 5\n5 5 5\n", "3", false),
            }),

        new("Balik Array", "array", ProblemLevel.Easy,
            "Baris pertama berisi `n`. Baris kedua berisi `n` bilangan bulat. " +
            "Cetak elemen dalam urutan terbalik, dipisah spasi.",
            new[]
            {
                ("5\n1 2 3 4 5\n", "5 4 3 2 1", true),
                ("1\n9\n", "9", false),
                ("3\n10 20 30\n", "30 20 10", false),
            }),

        new("Selisih Maks dan Min", "array", ProblemLevel.Easy,
            "Baris pertama berisi `n`. Baris kedua berisi `n` bilangan bulat. Cetak `nilai terbesar - nilai terkecil`.",
            new[]
            {
                ("5\n3 9 1 7 4\n", "8", true),
                ("1\n5\n", "0", false),
                ("4\n-3 -1 -9 -2\n", "8", false),
            }),

        new("Urutkan Array Menaik", "array,loop", ProblemLevel.Medium,
            "Baris pertama berisi `n` (1 ≤ n ≤ 1000). Baris kedua berisi `n` bilangan bulat. " +
            "Cetak elemen terurut dari terkecil ke terbesar, dipisah spasi.",
            new[]
            {
                ("5\n3 1 4 1 5\n", "1 1 3 4 5", true),
                ("1\n7\n", "7", false),
                ("4\n9 8 7 6\n", "6 7 8 9", false),
            }),

        // ---------- String ----------
        new("Panjang Kata", "string,input,output", ProblemLevel.Easy,
            "Baca sebuah kata (tanpa spasi). Cetak jumlah hurufnya.",
            new[]
            {
                ("halo\n", "4", true),
                ("a\n", "1", false),
                ("pemrograman\n", "11", false),
            }),

        new("Huruf Kapital", "string,loop", ProblemLevel.Easy,
            "Baca sebuah kata (huruf kecil). Cetak kata itu dalam huruf kapital semua.",
            new[]
            {
                ("halo\n", "HALO", true),
                ("bee\n", "BEE", false),
                ("abcxyz\n", "ABCXYZ", false),
            }),

        new("Hitung Huruf Vokal", "string,loop,selection", ProblemLevel.Medium,
            "Baca sebuah kata (huruf kecil). Cetak jumlah huruf vokal (`a`, `i`, `u`, `e`, `o`).",
            new[]
            {
                ("halo\n", "2", true),
                ("bcd\n", "0", false),
                ("aiueo\n", "5", false),
                ("pemrograman\n", "4", false),
            }),

        new("Balik Kata", "string,loop", ProblemLevel.Easy,
            "Baca sebuah kata. Cetak kata tersebut dengan urutan huruf dibalik.",
            new[]
            {
                ("halo\n", "olah", true),
                ("a\n", "a", false),
                ("abcd\n", "dcba", false),
            }),

        new("Cek Palindrom", "string,selection", ProblemLevel.Medium,
            "Baca sebuah kata. Cetak `Palindrom` bila dibaca dari depan sama dengan dari belakang, " +
            "selain itu `Bukan Palindrom`.",
            new[]
            {
                ("katak\n", "Palindrom", true),
                ("halo\n", "Bukan Palindrom", false),
                ("level\n", "Palindrom", false),
                ("a\n", "Palindrom", false),
            }),
    };
}
