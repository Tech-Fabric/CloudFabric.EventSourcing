mkdir -p tmp
cd tmp
curl -L -o sass_package.tar.gz https://github.com/sass/dart-sass/releases/download/1.55.0/dart-sass-1.55.0-linux-x64.tar.gz
tar -xvf ./sass_package.tar.gz
cp dart-sass/sass ~/.local/bin/
sudo cp dart-sass/sass /usr/local/bin/sass 
cd -
rm -Rf tmp