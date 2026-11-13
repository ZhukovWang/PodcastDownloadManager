# Podcast Download Manager - build helpers.
#
# Requires the .NET 10 SDK. On Windows, run this from a shell where GNU make is
# available (Git for Windows/MSYS2, scoop, chocolatey, ...).
#
#   make                 clean, restore, build and test (Release)
#   make build           restore and build (Release)
#   make test            run the test suite
#   make run ARGS="list" run the CLI from source
#   make publish         self-contained single-file build in artifacts/<rid>
#   make clean           remove bin/obj and publish output
#   make help            show this help

.DEFAULT_GOAL := release

DOTNET ?= dotnet
CONFIG ?= Release
TFM ?= net10.0

SOLUTION := PodcastDownloadManager.sln
APP_PROJECT := PodcastDownloadManager/PodcastDownloadManager.csproj
TEST_PROJECT := PodcastDownloadManager.Tests/PodcastDownloadManager.Tests.csproj
APP_OUTPUT := PodcastDownloadManager/bin/$(CONFIG)/$(TFM)

ARTIFACTS_DIR ?= artifacts
PUBLISH_FLAGS := -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true

ifeq ($(OS),Windows_NT)
RID ?= win-x64
APP_HOST := $(APP_OUTPUT)/pdlm.exe
RMDIR = powershell -NoProfile -Command "if (Test-Path '$(1)') { Remove-Item -Recurse -Force '$(1)' }"
else
RID ?= linux-x64
APP_HOST := $(APP_OUTPUT)/pdlm
RMDIR = rm -rf '$(1)'
endif

PUBLISH_DIR := $(ARTIFACTS_DIR)/$(RID)

.PHONY: all release build restore test run publish clean help

all: release

# The default: a from-scratch Release build, verified by the test suite.
release: clean
	$(MAKE) restore
	$(MAKE) build
	$(MAKE) test
	@echo Release build ready: $(APP_HOST)

restore:
	$(DOTNET) restore $(SOLUTION)

build: restore
	$(DOTNET) build $(SOLUTION) --configuration $(CONFIG) --no-restore

test: build
	$(DOTNET) run --project $(TEST_PROJECT) --configuration $(CONFIG) --no-build --no-restore

run: build
	$(DOTNET) run --project $(APP_PROJECT) --configuration $(CONFIG) --no-build --no-restore -- $(ARGS)

# A framework-independent, single-file executable (win-x64 on Windows).
publish:
	$(DOTNET) publish $(APP_PROJECT) --configuration $(CONFIG) --runtime $(RID) --self-contained true $(PUBLISH_FLAGS) --output $(PUBLISH_DIR)
	@echo Published to $(PUBLISH_DIR)

clean:
	@$(call RMDIR,PodcastDownloadManager/bin)
	@$(call RMDIR,PodcastDownloadManager/obj)
	@$(call RMDIR,PodcastDownloadManager.Tests/bin)
	@$(call RMDIR,PodcastDownloadManager.Tests/obj)
	@$(call RMDIR,$(ARTIFACTS_DIR))

help:
	@echo Usage:
	@echo make - clean, restore, build and test in Release
	@echo make build - restore and build
	@echo make test - run the test suite
	@echo make run ARGS=list - run the CLI from source
	@echo make publish - self-contained single-file build in $(PUBLISH_DIR)
	@echo make clean - remove bin/obj and publish output
	@echo Variables: CONFIG=$(CONFIG) RID=$(RID) DOTNET=$(DOTNET)
