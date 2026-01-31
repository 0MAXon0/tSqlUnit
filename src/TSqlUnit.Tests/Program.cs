using System;
using TSqlUnit.Tests;

var connectionString = @"Server=MAXon;Database=TEST;Integrated Security=true;TrustServerCertificate=True;";

try
{
    var test = new SimpleTest(connectionString);
    test.Run();
}
catch (Exception ex)
{
    Console.WriteLine($"Error: {ex.Message}");
}

Console.WriteLine("Done.");
Console.ReadKey();