.PHONY: app run install clean

app:
	@scripts/build-app.sh

run: app
	@pkill -x Ferry || true
	@open build/Ferry.app

install: app
	@pkill -x Ferry || true
	@rm -rf /Applications/Ferry.app
	@cp -R build/Ferry.app /Applications/Ferry.app
	@open /Applications/Ferry.app
	@echo "Installed to /Applications/Ferry.app"

clean:
	@rm -rf .build build
