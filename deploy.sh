#!/bin/bash

# Color definitions
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
BLUE='\033[0;34m'
NC='\033[0m' # No Color

# Function to print custom messages
print_message() {
    local color=$1
    local message=$2
    echo -e "${color}[ INFO ] ${message}${NC}"
}

# Function to build a project
build_project() {
    local project=$1
    local name=$2
    
    print_message "${YELLOW}" "Building $name..."
    dotnet restore "$project" --verbosity quiet
    if [ $? -ne 0 ]; then
        print_message "${RED}" "dotnet restore failed for $name"
        exit 1
    fi
    
    dotnet publish "$project" -f net8.0 -c Release --verbosity quiet
    if [ $? -ne 0 ]; then
        print_message "${RED}" "dotnet publish failed for $name"
        exit 1
    fi
}

# Clean up previous build
print_message "${BLUE}" "Cleaning up previous build..."
rm -rf ./Zenith

# Create directory structure
print_message "${BLUE}" "Creating directory structure..."
mkdir -p ./Zenith/plugins/K4-Zenith \
         ./Zenith/shared/K4-ZenithAPI \
         ./Zenith/plugins/K4-Zenith-TimeStats \
         ./Zenith/plugins/K4-Zenith-Ranks \
         ./Zenith/plugins/K4-Zenith-Stats \
         ./Zenith/plugins/K4-Zenith-CustomTags \
         ./Zenith/plugins/K4-Zenith-Toplists

# Build in correct order: API first, then main plugin, then modules
print_message "${BLUE}" "Starting build process..."

# 1. Build API first (required by everything else)
build_project "./src-api/K4-ZenithAPI.csproj" "ZenithAPI"

# 2. Build main plugin (depends on API)
build_project "./src/K4-Zenith.csproj" "K4-Zenith"

# 3. Build modules (depend on API)
build_project "./modules/time-stats/K4-Zenith-TimeStats.csproj" "TimeStats"
build_project "./modules/ranks/K4-Zenith-Ranks.csproj" "Ranks"
build_project "./modules/statistics/K4-Zenith-Stats.csproj" "Statistics"
build_project "./modules/custom-tags/K4-Zenith-CustomTags.csproj" "CustomTags"
build_project "./modules/toplists/K4-Zenith-Toplists.csproj" "Toplists"

print_message "${GREEN}" "All projects built successfully!"

# Copy files to output directory
print_message "${BLUE}" "Copying files to output directory..."

# Copy main plugin
print_message "${YELLOW}" "Copying main plugin files..."
cp -r ./src/bin/K4-Zenith/plugins/K4-Zenith/* ./Zenith/plugins/K4-Zenith/

# Copy shared files
print_message "${YELLOW}" "Copying shared files..."
cp -r ./src/bin/K4-Zenith/shared/* ./Zenith/shared/
cp -r ./src-api/bin/K4-ZenithAPI/* ./Zenith/shared/K4-ZenithAPI/

# Copy modules
print_message "${YELLOW}" "Copying modules..."
cp -r ./modules/time-stats/bin/K4-Zenith-TimeStats/* ./Zenith/plugins/K4-Zenith-TimeStats/
cp -r ./modules/ranks/bin/K4-Zenith-Ranks/* ./Zenith/plugins/K4-Zenith-Ranks/
cp -r ./modules/statistics/bin/K4-Zenith-Stats/* ./Zenith/plugins/K4-Zenith-Stats/
cp -r ./modules/custom-tags/bin/K4-Zenith-CustomTags/* ./Zenith/plugins/K4-Zenith-CustomTags/
cp -r ./modules/toplists/bin/K4-Zenith-Toplists/* ./Zenith/plugins/K4-Zenith-Toplists/

# Download GeoLite2 database
print_message "${YELLOW}" "Downloading GeoLite2-Country.mmdb..."
curl -sL https://github.com/P3TERX/GeoLite.mmdb/releases/latest/download/GeoLite2-Country.mmdb -o ./Zenith/plugins/K4-Zenith/GeoLite2-Country.mmdb

# Clean up unnecessary files
print_message "${BLUE}" "Cleaning up unnecessary files..."
find ./Zenith -type f \( -name "*.pdb" -o -name "*.yaml" -o -name ".DS_Store" \) -delete 2>/dev/null

# Clean up build directories
print_message "${BLUE}" "Cleaning up build directories..."
find ./src ./src-api ./modules -type d -name "bin" -exec rm -rf {} + 2>/dev/null
find ./src ./src-api ./modules -type d -name "obj" -exec rm -rf {} + 2>/dev/null

print_message "${GREEN}" "Deployment completed successfully!"
