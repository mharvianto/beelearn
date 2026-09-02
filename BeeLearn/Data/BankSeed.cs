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
    };
}
