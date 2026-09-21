using System;
using System.Collections.Generic;
using System.IO;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using Microsoft.Extensions.DependencyInjection;
using StudyDocumentManager.Core.DTOs;
using StudyDocumentManager.Core.Entities;
using StudyDocumentManager.Core.Interfaces;
using StudyDocumentManager.Models;
using StudyDocumentManager.Services;
using StudyDocumentManager.Views;
using Xunit;

namespace StudyDocumentManager.Tests;

public class UpdateBadgeScreenshotTests
{
    private static string GetScreenshotDirectory()
    {
        var customDir = Environment.GetEnvironmentVariable("SDM_SCREENSHOT_DIR");
        return !string.IsNullOrWhiteSpace(customDir)
            ? customDir
            : Path.Combine(Path.GetTempPath(), "sdm_screenshots");
    }

    private static void SaveWindowBitmap(Window window, int width, int height, string filename)
    {
        Dispatcher.UIThread.RunJobs();
        var pixelSize = new PixelSize(width, height);
        var dpi = new Vector(96, 96);
        using var bitmap = new RenderTargetBitmap(pixelSize, dpi);
        bitmap.Render(window);

        var dir = GetScreenshotDirectory();
        try
        {
            Directory.CreateDirectory(dir);
            bitmap.Save(Path.Combine(dir, filename));
        }
        catch
        {
            // 保存に失敗してもテスト自体は続行する
        }
    }

    [AvaloniaFact]
    public void Capture_UpdateBadgeScreenshots()
    {
        var outputDir = GetScreenshotDirectory();
        Directory.CreateDirectory(outputDir);

        var docRepo = App.Services!.GetRequiredService<IDocumentRepository>();
        var officeRepo = App.Services!.GetRequiredService<IOfficeMetadataRepository>();
        var assignRepo = App.Services!.GetRequiredService<IAssignmentRepository>();
        var mainModel = App.Services!.GetRequiredService<MainWindowModel>();
        var navService = App.Services!.GetRequiredService<NavigationService>();

        int? createdDocId = null;
        int? createdAssignmentId = null;
        var createdCourseIds = new List<int>();
        var createdSemesterIds = new List<int>();

        try
        {
            if (docRepo.GetAll().Count == 0)
            {
                var doc = new StudyDocument
                {
                    Name = "契約書_ドラフト_v2.pdf",
                    FilePath = Path.Combine(Path.GetTempPath(), $"契約書_ドラフト_{Guid.NewGuid():N}.pdf"),
                    Subject = "法務",
                    Type = "PDF",
                    Status = DocumentStatus.InProgress,
                    CreatedAt = DateTime.UtcNow
                };
                docRepo.Add(doc);
                var allDocs = docRepo.GetAll();
                var createdDoc = allDocs.Find(d => d.Name == doc.Name && d.FilePath == doc.FilePath);
                if (createdDoc != null)
                {
                    createdDocId = createdDoc.Id;
                    officeRepo.Save(new OfficeDocumentMetadata
                    {
                        DocumentId = createdDocId.Value,
                        DocumentNumber = "DOC-2026-089",
                        OrganizationOrProject = "プロジェクトアルファ",
                        ContactName = "山田 太郎",
                        EffectiveDate = DateTime.Today,
                        ExpiryDate = DateTime.Today.AddDays(30),
                        ConfidentialityLevel = OfficeConfidentialityLevel.Internal,
                        ReminderEnabled = true,
                        ReminderDaysBefore = 7
                    });
                }
            }

            if (assignRepo.GetCourses().Count == 0)
            {
                createdCourseIds.Add(assignRepo.AddCourse(new Course { Name = "コンピュータサイエンス基礎" }));
                createdCourseIds.Add(assignRepo.AddCourse(new Course { Name = "データ構造とアルゴリズム" }));
            }
            if (assignRepo.GetSemesters().Count == 0)
            {
                createdSemesterIds.Add(assignRepo.AddSemester(new Semester { Name = "2026年 前期", IsActive = true }));
            }
            if (assignRepo.GetAssignments().Count == 0)
            {
                var sems = assignRepo.GetSemesters();
                var courses = assignRepo.GetCourses();
                createdAssignmentId = assignRepo.AddAssignment(new Assignment
                {
                    Title = "アルゴリズム第3回復習レポート",
                    CourseId = courses.Count > 0 ? courses[0].Id : null,
                    SemesterId = sems.Count > 0 ? sems[0].Id : 1,
                    OfficialDeadline = DateTime.Today.AddDays(7),
                    PersonalDeadline = DateTime.Today.AddDays(5),
                    Status = AssignmentStatuses.InProgress,
                    Priority = AssignmentPriorities.High,
                    Milestone = "ドラフト提出",
                    Notes = "第3章の計算量解析を重点的にまとめること。"
                });
            }

            navService.SetMainModel(mainModel);

            // Simulate update available v4.2.0
            mainModel.HasUpdateAvailable = true;
            mainModel.UpdateVersionText = "v4.2.0";
            mainModel.LatestUpdateInfo = new UpdateInfo
            {
                HasUpdate = true,
                NewVersion = "v4.2.0"
            };

            // 1. Dashboard with Update Badge
            {
                navService.NavigateTo("dashboard");
                var window = new MainWindow
                {
                    DataContext = mainModel,
                    Width = 1280,
                    Height = 800
                };
                window.Show();
                Dispatcher.UIThread.RunJobs();
                SaveWindowBitmap(window, 1280, 800, "00_MainWindow_UpdateBadge_Dashboard.png");
                window.Close();
                Dispatcher.UIThread.RunJobs();
            }

            // 2. OfficeWorkspace with Update Badge & Global Back button
            {
                navService.NavigateTo("office-workspace");
                var officeModel = (OfficeWorkspaceModel)mainModel.CurrentView;
                officeModel.Refresh();
                if (officeModel.FilteredRows.Count > 0)
                {
                    officeModel.SelectedRow = officeModel.FilteredRows[0];
                }
                var window = new MainWindow
                {
                    DataContext = mainModel,
                    Width = 1280,
                    Height = 800
                };
                window.Show();
                Dispatcher.UIThread.RunJobs();
                SaveWindowBitmap(window, 1280, 800, "00_MainWindow_UpdateBadge_OfficeWorkspace.png");
                window.Close();
                Dispatcher.UIThread.RunJobs();
            }

            // 3. StudentWorkspace with Update Badge & Global Back button
            {
                navService.NavigateTo("student-workspace");
                var studentModel = (StudentWorkspaceModel)mainModel.CurrentView;
                studentModel.RefreshCommand.Execute(null);
                if (studentModel.Assignments.Count > 0)
                {
                    studentModel.SelectedAssignment = studentModel.Assignments[0];
                }
                var window = new MainWindow
                {
                    DataContext = mainModel,
                    Width = 1280,
                    Height = 800
                };
                window.Show();
                Dispatcher.UIThread.RunJobs();
                SaveWindowBitmap(window, 1280, 800, "00_MainWindow_UpdateBadge_StudentWorkspace.png");
                window.Close();
                Dispatcher.UIThread.RunJobs();
            }
        }
        finally
        {
            // 状態のクリーンアップ（シングルトンモデルのリセットとエンティティの削除）
            mainModel.HasUpdateAvailable = false;
            mainModel.UpdateVersionText = string.Empty;
            mainModel.LatestUpdateInfo = null;
            navService.NavigateTo("dashboard");

            if (createdDocId.HasValue)
            {
                officeRepo.DeleteByDocumentId(createdDocId.Value);
                docRepo.Delete(createdDocId.Value);
            }

            if (createdAssignmentId.HasValue)
            {
                assignRepo.DeleteAssignment(createdAssignmentId.Value);
            }

            foreach (var courseId in createdCourseIds)
            {
                assignRepo.DeleteCourse(courseId);
            }

            foreach (var semesterId in createdSemesterIds)
            {
                assignRepo.DeleteSemester(semesterId);
            }
        }
    }
}
