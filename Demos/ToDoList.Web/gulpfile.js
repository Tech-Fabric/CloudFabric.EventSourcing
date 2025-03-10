const gulp = require('gulp');
const sass = require('gulp-sass')(require('sass'));
const concat = require('gulp-concat');
const uglify = require('gulp-uglify');
const browserSync = require('browser-sync').create();
const fs = require('fs');

function getPort() {
    const launchsettingsString = fs.readFileSync('./Properties/launchSettings.json', 'utf8').trim();
    console.log(launchsettingsString);
    const launchSettings = JSON.parse(launchsettingsString);
    const applicationUrl = launchSettings.profiles["http"].applicationUrl;
    const url = new URL(applicationUrl);
    return url.port;
}

// Array of JavaScript files to bundle
const jsFiles = [
    'node_modules/bootstrap/dist/js/bootstrap.bundle.min.js',
    'node_modules/htmx.org/dist/htmx.min.js',
    'node_modules/sortablejs/Sortable.min.js'
];

// Compile SASS files
gulp.task('sass', function() {
    return gulp.src('./Scss/Index.scss')
        .pipe(sass.sync({style: 'compressed'}).on('error', sass.logError))
        .pipe(gulp.dest('./wwwroot/css/'))
        .pipe(browserSync.stream());
});

// Compile, merge, and minify JavaScript libraries
gulp.task('scripts', function() {
    return gulp.src(jsFiles)
        .pipe(concat('bundle.js'))
        .pipe(uglify())
        .pipe(gulp.dest('./wwwroot/js'))
        .pipe(browserSync.stream());
});

// BrowserSync to reload the browser
gulp.task('serve', function() {
    const port = getPort();
    browserSync.init({
        proxy: `http://localhost:${port}`
    });

    gulp.watch('./Views/**/*.scss', gulp.series('sass'));
    gulp.watch(jsFiles, gulp.series('scripts'));
    gulp.watch(['./**/browsersync-update.txt', './**/*.cshtml']).on('change', browserSync.reload);
});

// Default task to run everything
gulp.task('default', gulp.series('sass', 'scripts', 'serve'));