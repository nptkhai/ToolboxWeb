CREATE TABLE "AspNetRoles" (
    "Id" TEXT NOT NULL CONSTRAINT "PK_AspNetRoles" PRIMARY KEY,
    "Name" TEXT NULL,
    "NormalizedName" TEXT NULL,
    "ConcurrencyStamp" TEXT NULL
);


CREATE TABLE "AspNetUsers" (
    "Id" TEXT NOT NULL CONSTRAINT "PK_AspNetUsers" PRIMARY KEY,
    "CreatedAt" TEXT NOT NULL,
    "AvatarUrl" TEXT NULL,
    "UserName" TEXT NULL,
    "NormalizedUserName" TEXT NULL,
    "Email" TEXT NULL,
    "NormalizedEmail" TEXT NULL,
    "EmailConfirmed" INTEGER NOT NULL,
    "PasswordHash" TEXT NULL,
    "SecurityStamp" TEXT NULL,
    "ConcurrencyStamp" TEXT NULL,
    "PhoneNumber" TEXT NULL,
    "PhoneNumberConfirmed" INTEGER NOT NULL,
    "TwoFactorEnabled" INTEGER NOT NULL,
    "LockoutEnd" TEXT NULL,
    "LockoutEnabled" INTEGER NOT NULL,
    "AccessFailedCount" INTEGER NOT NULL
);


CREATE TABLE "AspNetRoleClaims" (
    "Id" INTEGER NOT NULL CONSTRAINT "PK_AspNetRoleClaims" PRIMARY KEY AUTOINCREMENT,
    "RoleId" TEXT NOT NULL,
    "ClaimType" TEXT NULL,
    "ClaimValue" TEXT NULL,
    CONSTRAINT "FK_AspNetRoleClaims_AspNetRoles_RoleId" FOREIGN KEY ("RoleId") REFERENCES "AspNetRoles" ("Id") ON DELETE CASCADE
);


CREATE TABLE "ActivityLogs" (
    "Id" INTEGER NOT NULL CONSTRAINT "PK_ActivityLogs" PRIMARY KEY AUTOINCREMENT,
    "UserId" TEXT NOT NULL,
    "ActionType" INTEGER NOT NULL,
    "EntityType" INTEGER NOT NULL,
    "EntityId" TEXT NULL,
    "Summary" TEXT NOT NULL,
    "CreatedAt" TEXT NOT NULL,
    CONSTRAINT "FK_ActivityLogs_AspNetUsers_UserId" FOREIGN KEY ("UserId") REFERENCES "AspNetUsers" ("Id") ON DELETE CASCADE
);


CREATE TABLE "AspNetUserClaims" (
    "Id" INTEGER NOT NULL CONSTRAINT "PK_AspNetUserClaims" PRIMARY KEY AUTOINCREMENT,
    "UserId" TEXT NOT NULL,
    "ClaimType" TEXT NULL,
    "ClaimValue" TEXT NULL,
    CONSTRAINT "FK_AspNetUserClaims_AspNetUsers_UserId" FOREIGN KEY ("UserId") REFERENCES "AspNetUsers" ("Id") ON DELETE CASCADE
);


CREATE TABLE "AspNetUserLogins" (
    "LoginProvider" TEXT NOT NULL,
    "ProviderKey" TEXT NOT NULL,
    "ProviderDisplayName" TEXT NULL,
    "UserId" TEXT NOT NULL,
    CONSTRAINT "PK_AspNetUserLogins" PRIMARY KEY ("LoginProvider", "ProviderKey"),
    CONSTRAINT "FK_AspNetUserLogins_AspNetUsers_UserId" FOREIGN KEY ("UserId") REFERENCES "AspNetUsers" ("Id") ON DELETE CASCADE
);


CREATE TABLE "AspNetUserRoles" (
    "UserId" TEXT NOT NULL,
    "RoleId" TEXT NOT NULL,
    CONSTRAINT "PK_AspNetUserRoles" PRIMARY KEY ("UserId", "RoleId"),
    CONSTRAINT "FK_AspNetUserRoles_AspNetRoles_RoleId" FOREIGN KEY ("RoleId") REFERENCES "AspNetRoles" ("Id") ON DELETE CASCADE,
    CONSTRAINT "FK_AspNetUserRoles_AspNetUsers_UserId" FOREIGN KEY ("UserId") REFERENCES "AspNetUsers" ("Id") ON DELETE CASCADE
);


CREATE TABLE "AspNetUserTokens" (
    "UserId" TEXT NOT NULL,
    "LoginProvider" TEXT NOT NULL,
    "Name" TEXT NOT NULL,
    "Value" TEXT NULL,
    CONSTRAINT "PK_AspNetUserTokens" PRIMARY KEY ("UserId", "LoginProvider", "Name"),
    CONSTRAINT "FK_AspNetUserTokens_AspNetUsers_UserId" FOREIGN KEY ("UserId") REFERENCES "AspNetUsers" ("Id") ON DELETE CASCADE
);


CREATE TABLE "ChecklistItems" (
    "Id" INTEGER NOT NULL CONSTRAINT "PK_ChecklistItems" PRIMARY KEY AUTOINCREMENT,
    "UserId" TEXT NOT NULL,
    "Title" TEXT NOT NULL,
    "Description" TEXT NULL,
    "Status" INTEGER NOT NULL,
    "DueDate" TEXT NULL,
    "CreatedAt" TEXT NOT NULL,
    "CompletedAt" TEXT NULL,
    CONSTRAINT "FK_ChecklistItems_AspNetUsers_UserId" FOREIGN KEY ("UserId") REFERENCES "AspNetUsers" ("Id") ON DELETE CASCADE
);


CREATE TABLE "FocusSessions" (
    "Id" INTEGER NOT NULL CONSTRAINT "PK_FocusSessions" PRIMARY KEY AUTOINCREMENT,
    "UserId" TEXT NOT NULL,
    "DurationMinutes" INTEGER NOT NULL,
    "StartedAt" TEXT NOT NULL,
    "CompletedAt" TEXT NOT NULL,
    "Note" TEXT NULL,
    CONSTRAINT "FK_FocusSessions_AspNetUsers_UserId" FOREIGN KEY ("UserId") REFERENCES "AspNetUsers" ("Id") ON DELETE CASCADE
);


CREATE TABLE "Notes" (
    "Id" INTEGER NOT NULL CONSTRAINT "PK_Notes" PRIMARY KEY AUTOINCREMENT,
    "UserId" TEXT NOT NULL,
    "Title" TEXT NOT NULL,
    "Content" TEXT NOT NULL,
    "Tags" TEXT NULL,
    "IsPinned" INTEGER NOT NULL,
    "CreatedAt" TEXT NOT NULL,
    "UpdatedAt" TEXT NULL,
    CONSTRAINT "FK_Notes_AspNetUsers_UserId" FOREIGN KEY ("UserId") REFERENCES "AspNetUsers" ("Id") ON DELETE CASCADE
);


CREATE TABLE "PromptTemplates" (
    "Id" INTEGER NOT NULL CONSTRAINT "PK_PromptTemplates" PRIMARY KEY AUTOINCREMENT,
    "UserId" TEXT NOT NULL,
    "Name" TEXT NOT NULL,
    "Category" INTEGER NOT NULL,
    "Description" TEXT NULL,
    "InputVariables" TEXT NULL,
    "OutputFormat" TEXT NULL,
    "Content" TEXT NOT NULL,
    "UseCount" INTEGER NOT NULL,
    "CreatedAt" TEXT NOT NULL,
    "UpdatedAt" TEXT NULL,
    "LastUsedAt" TEXT NULL,
    CONSTRAINT "FK_PromptTemplates_AspNetUsers_UserId" FOREIGN KEY ("UserId") REFERENCES "AspNetUsers" ("Id") ON DELETE CASCADE
);


CREATE TABLE "QuickLinks" (
    "Id" INTEGER NOT NULL CONSTRAINT "PK_QuickLinks" PRIMARY KEY AUTOINCREMENT,
    "UserId" TEXT NOT NULL,
    "Title" TEXT NOT NULL,
    "Url" TEXT NOT NULL,
    "GroupName" TEXT NULL,
    "SortOrder" INTEGER NOT NULL,
    "CreatedAt" TEXT NOT NULL,
    CONSTRAINT "FK_QuickLinks_AspNetUsers_UserId" FOREIGN KEY ("UserId") REFERENCES "AspNetUsers" ("Id") ON DELETE CASCADE
);


CREATE TABLE "TabulatorTasks" (
    "Id" INTEGER NOT NULL CONSTRAINT "PK_TabulatorTasks" PRIMARY KEY AUTOINCREMENT,
    "UserId" TEXT NOT NULL,
    "Title" TEXT NOT NULL,
    "Category" INTEGER NOT NULL,
    "Status" INTEGER NOT NULL,
    "Priority" INTEGER NOT NULL,
    "Progress" INTEGER NOT NULL,
    "Budget" decimal(18,2) NOT NULL,
    "DueDate" TEXT NULL,
    "Note" TEXT NULL,
    "CreatedAt" TEXT NOT NULL,
    "UpdatedAt" TEXT NULL,
    CONSTRAINT "FK_TabulatorTasks_AspNetUsers_UserId" FOREIGN KEY ("UserId") REFERENCES "AspNetUsers" ("Id") ON DELETE CASCADE
);


CREATE INDEX "IX_ActivityLogs_UserId_CreatedAt" ON "ActivityLogs" ("UserId", "CreatedAt");


CREATE INDEX "IX_AspNetRoleClaims_RoleId" ON "AspNetRoleClaims" ("RoleId");


CREATE UNIQUE INDEX "RoleNameIndex" ON "AspNetRoles" ("NormalizedName");


CREATE INDEX "IX_AspNetUserClaims_UserId" ON "AspNetUserClaims" ("UserId");


CREATE INDEX "IX_AspNetUserLogins_UserId" ON "AspNetUserLogins" ("UserId");


CREATE INDEX "IX_AspNetUserRoles_RoleId" ON "AspNetUserRoles" ("RoleId");


CREATE INDEX "EmailIndex" ON "AspNetUsers" ("NormalizedEmail");


CREATE UNIQUE INDEX "UserNameIndex" ON "AspNetUsers" ("NormalizedUserName");


CREATE INDEX "IX_ChecklistItems_UserId_Status_DueDate" ON "ChecklistItems" ("UserId", "Status", "DueDate");


CREATE INDEX "IX_FocusSessions_UserId_CompletedAt" ON "FocusSessions" ("UserId", "CompletedAt");


CREATE INDEX "IX_Notes_UserId_IsPinned_UpdatedAt" ON "Notes" ("UserId", "IsPinned", "UpdatedAt");


CREATE INDEX "IX_PromptTemplates_UserId_Category_UpdatedAt" ON "PromptTemplates" ("UserId", "Category", "UpdatedAt");


CREATE INDEX "IX_QuickLinks_UserId_GroupName_SortOrder" ON "QuickLinks" ("UserId", "GroupName", "SortOrder");


CREATE INDEX "IX_TabulatorTasks_UserId_Status_DueDate" ON "TabulatorTasks" ("UserId", "Status", "DueDate");


