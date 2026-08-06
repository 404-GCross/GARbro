#!/usr/bin/env sh
set -eu

if [ "$#" -lt 2 ]; then
  echo "Usage: $0 PUBLISH_DIR OUTPUT_DIR [VERSION]" >&2
  exit 2
fi

PUBLISH_DIR=$(CDPATH= cd -- "$1" && pwd)
OUTPUT_DIR=$2
VERSION=${3:-dev}
APP_ID=garbro-linux
APP_NAME="GARbro Linux"
EXECUTABLE=GARbro.GUI.Linux
INSTALL_DIR=/opt/garbro-linux
ICON=garbro-linux.svg

mkdir -p "$OUTPUT_DIR"
OUTPUT_DIR=$(CDPATH= cd -- "$OUTPUT_DIR" && pwd)

WORK_DIR=$(mktemp -d)
cleanup() {
  rm -rf "$WORK_DIR"
}
trap cleanup EXIT INT TERM

package_version() {
  value=$(printf '%s' "$1" | sed 's/[^A-Za-z0-9.+:~_-]/_/g')
  case "$value" in
    [0-9]*) printf '%s\n' "$value" ;;
    *) printf '0.0.0+%s\n' "$value" ;;
  esac
}

copy_payload() {
  target=$1
  mkdir -p "$target"
  cp -a "$PUBLISH_DIR"/. "$target"/
  chmod +x "$target/$EXECUTABLE" 2>/dev/null || true
  chmod +x "$target/install-desktop.sh" 2>/dev/null || true
}

write_desktop() {
  target=$1
  exec_line=$2
  sed "s#^Exec=.*#Exec=$exec_line %f#" \
    "$PUBLISH_DIR/GARbro.GUI.Linux.desktop" > "$target"
}

install_icon() {
  root=$1
  if [ -f "$PUBLISH_DIR/$ICON" ]; then
    mkdir -p "$root/usr/share/icons/hicolor/scalable/apps"
    cp "$PUBLISH_DIR/$ICON" "$root/usr/share/icons/hicolor/scalable/apps/$ICON"
  fi
}

make_tarball() {
  payload="$WORK_DIR/$APP_ID-$VERSION"
  copy_payload "$payload"
  tar -C "$WORK_DIR" -czf "$OUTPUT_DIR/GARbro-Linux-GUI.tar.gz" "$APP_ID-$VERSION"
}

make_deb() {
  if ! command -v dpkg-deb >/dev/null 2>&1; then
    echo "dpkg-deb not found; skipping deb package" >&2
    return 0
  fi

  root="$WORK_DIR/deb"
  mkdir -p "$root/DEBIAN" "$root$INSTALL_DIR" "$root/usr/share/applications" "$root/usr/bin"
  copy_payload "$root$INSTALL_DIR"
  write_desktop "$root/usr/share/applications/$APP_ID.desktop" "$INSTALL_DIR/$EXECUTABLE"
  install_icon "$root"
  ln -s "$INSTALL_DIR/$EXECUTABLE" "$root/usr/bin/$EXECUTABLE"
  installed_size=$(du -sk "$root" | awk '{print $1}')
  deb_version=$(package_version "$VERSION")
  cat > "$root/DEBIAN/control" <<EOF
Package: garbro-linux
Version: $deb_version
Section: utils
Priority: optional
Architecture: amd64
Maintainer: 404-GCross <404-GCross@users.noreply.github.com>
Installed-Size: $installed_size
Description: GARbro native Linux GUI
 Browse and extract visual novel resource archives.
EOF
  dpkg-deb --build "$root" "$OUTPUT_DIR/GARbro-Linux-GUI.deb" >/dev/null
}

make_rpm() {
  if ! command -v rpmbuild >/dev/null 2>&1; then
    echo "rpmbuild not found; skipping rpm package" >&2
    return 0
  fi

  rpm_version=$(package_version "$VERSION" | sed 's/[^A-Za-z0-9._~-]/_/g')
  top="$WORK_DIR/rpmbuild"
  payload="$top/payload"
  mkdir -p "$top/BUILD" "$top/RPMS" "$top/SOURCES" "$top/SPECS" "$top/SRPMS"
  mkdir -p "$payload$INSTALL_DIR" "$payload/usr/share/applications" "$payload/usr/bin"
  copy_payload "$payload$INSTALL_DIR"
  write_desktop "$payload/usr/share/applications/$APP_ID.desktop" "$INSTALL_DIR/$EXECUTABLE"
  install_icon "$payload"
  ln -s "$INSTALL_DIR/$EXECUTABLE" "$payload/usr/bin/$EXECUTABLE"

  cat > "$top/SPECS/garbro-linux.spec" <<EOF
Name: garbro-linux
Version: $rpm_version
Release: 1
Summary: GARbro native Linux GUI
License: MIT
BuildArch: x86_64

%description
Browse and extract visual novel resource archives.

%install
rm -rf "%{buildroot}"
mkdir -p "%{buildroot}"
cp -a "$payload"/. "%{buildroot}"/

%files
$INSTALL_DIR
/usr/bin/$EXECUTABLE
/usr/share/applications/$APP_ID.desktop
/usr/share/icons/hicolor/scalable/apps/$ICON

%changelog
* Thu Aug 06 2026 404-GCross <404-GCross@users.noreply.github.com> - $rpm_version-1
- Automated Linux development package.
EOF
  rpmbuild --define "_topdir $top" -bb "$top/SPECS/garbro-linux.spec" >/dev/null
  cp "$top"/RPMS/x86_64/*.rpm "$OUTPUT_DIR/GARbro-Linux-GUI.rpm"
}

make_appdir() {
  appdir="$WORK_DIR/GARbro-Linux-GUI.AppDir"
  mkdir -p "$appdir/usr/lib/garbro-linux" "$appdir/usr/share/applications" "$appdir/usr/bin"
  copy_payload "$appdir/usr/lib/garbro-linux"
  write_desktop "$appdir/GARbro.GUI.Linux.desktop" "GARbro.GUI.Linux"
  cp "$appdir/GARbro.GUI.Linux.desktop" "$appdir/usr/share/applications/$APP_ID.desktop"
  install_icon "$appdir"
  if [ -f "$PUBLISH_DIR/$ICON" ]; then
    cp "$PUBLISH_DIR/$ICON" "$appdir/$ICON"
  fi
  ln -s "../lib/garbro-linux/$EXECUTABLE" "$appdir/usr/bin/$EXECUTABLE"
  cat > "$appdir/AppRun" <<'EOF'
#!/usr/bin/env sh
HERE=$(CDPATH= cd -- "$(dirname -- "$0")" && pwd)
exec "$HERE/usr/lib/garbro-linux/GARbro.GUI.Linux" "$@"
EOF
  chmod +x "$appdir/AppRun"
  tar -C "$WORK_DIR" -czf "$OUTPUT_DIR/GARbro-Linux-GUI-AppDir.tar.gz" "GARbro-Linux-GUI.AppDir"

  tool=${APPIMAGETOOL:-}
  if [ -n "$tool" ] && [ -x "$tool" ]; then
    ARCH=x86_64 APPIMAGE_EXTRACT_AND_RUN=1 "$tool" "$appdir" "$OUTPUT_DIR/GARbro-Linux-GUI.AppImage" >/dev/null
    chmod +x "$OUTPUT_DIR/GARbro-Linux-GUI.AppImage"
  else
    echo "APPIMAGETOOL not set or not executable; skipping AppImage" >&2
  fi
}

make_tarball
make_deb
make_rpm
make_appdir

echo "Created Linux GUI packages in $OUTPUT_DIR"
