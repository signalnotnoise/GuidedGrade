# Sample submissions

All names, files, and any seeded review feedback are synthetic demonstration data.
Open `Demo/Submissions` using **Open submissions**, choose Example, Alex, then expand
Lab_04_Conditions. Open Program.cs or right-click ConditionsLab.slnx for Programming tools.
The first submission intentionally lacks range validation; the second includes it.
Both solutions are real .NET 10 console projects and require no external packages.

Snapshot check: run FrameworkMigrationTests.SampleDirectoryRendersRealFilesAndMenus.
It loads these on-disk files through the app navigation, seeds explicitly labeled demo
feedback into a temporary database, and writes rendered snapshots to artifacts/ui-migration.
It does not call AI, change your saved assignments/reviews, or run student code.
