#!/usr/bin/env bash
set -euo pipefail

repo_root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
version=""

fail() {
    printf '%s\n' "$*" >&2
    exit 1
}

while (($#)); do
    case "$1" in
        --version)
            (($# >= 2)) || fail "--version requires a value"
            version="$2"
            shift 2
            ;;
        *)
            fail "Usage: $0 [--version VERSION]"
            ;;
    esac
done

for command in dotnet dpkg dpkg-deb; do
    command -v "$command" >/dev/null 2>&1 || fail "Required command not found: $command"
done

app_version_file="$repo_root/StudyDocumentManager.Core/Services/AppVersion.cs"
app_version="$(sed -nE 's/.*Current => "([^"]+)".*/\1/p' "$app_version_file" | head -n 1)"
[ -n "$app_version" ] || fail "Could not read AppVersion.Current from $app_version_file"
version="${version:-$app_version}"
[ "$version" = "$app_version" ] || fail "Version '$version' does not match AppVersion.Current '$app_version'"
dpkg --validate-version "$version" || fail "Invalid Debian package version: $version"

publish_dir="$repo_root/artifacts/publish/linux-x64"
output_dir="$repo_root/artifacts/debian"
template_dir="$repo_root/packaging/debian"
output_file="$output_dir/document-manager_${version}_amd64.deb"

mkdir -p "$output_dir"
package_dir="$(mktemp -d "$output_dir/.document-manager.XXXXXX")"
trap 'rm -rf "$package_dir"' EXIT

rm -rf "$publish_dir"
dotnet publish "$repo_root/StudyDocumentManager/StudyDocumentManager.csproj" \
    -c Release \
    -r linux-x64 \
    --self-contained true \
    -p:PublishSingleFile=false \
    -p:PublishTrimmed=false \
    -p:DebugType=None \
    -p:DebugSymbols=false \
    -p:Version="$version" \
    -o "$publish_dir"

app_binary="$publish_dir/DocumentManager"
[ -x "$app_binary" ] || fail "Linux publish output is missing executable $app_binary. Apply the Linux platform support changes before packaging."

cp -a "$template_dir/." "$package_dir/"
sed -i "s/@VERSION@/$version/g" "$package_dir/DEBIAN/control"
install -d "$package_dir/usr/lib/document-manager"
cp -a "$publish_dir/." "$package_dir/usr/lib/document-manager/"
chmod 0755 "$package_dir/usr/bin/document-manager" "$package_dir/usr/lib/document-manager/DocumentManager"
rm -f "$output_file"
dpkg-deb --build --root-owner-group "$package_dir" "$output_file"

dpkg-deb --info "$output_file"
dpkg-deb --contents "$output_file" | grep -q 'usr/lib/document-manager/DocumentManager$'
dpkg-deb --contents "$output_file" | grep -q 'usr/bin/document-manager$'
dpkg-deb --contents "$output_file" | grep -q 'usr/share/applications/document-manager.desktop$'
dpkg-deb --contents "$output_file" | grep -q 'usr/share/icons/hicolor/scalable/apps/document-manager.svg$'
printf 'DebianPackage=%s\n' "$output_file"
