#! /bin/bash

wd="$( pwd )";

pushd $(dirname $0)                                                                                     2> /dev/null  > /dev/null

echo "PWD= $( pwd )"
echo "ARGV= $@"

# go to root of project
cd ..

rootdir="$( pwd )";


getopts ":bedfsch" opt
#echo opt+arg = "$opt$OPTARG"
case "$opt$OPTARG" in
b )
  echo "--- (re)build site ---"

  rm -rf docs/
  mkdir docs
  #DEBUG="*,-not_this" npx @gerhobbelt/eleventy --config=docs-src/.eleventy.js
  #prettier --write docs/

  #node docs-src/site-builder.js
  
  #node node_modules/deGaulle/dist/cli.js build docs-src/ --output ./docs/
  node ../deGaulle/dist/cli.js build docs-src/ --output ./docs/ --config docs-src/site-builder.mjs

  echo done.
  ;;

e )
  echo "--- (re)build site using eleventy ---"

  rm -rf docs/
  mkdir docs
  DEBUG="*,-not_this" npx @gerhobbelt/eleventy --config=docs-src/.eleventy.js
  #prettier --write docs/

  #node docs-src/site-builder.js

  echo done.
  ;;

d )
  echo "--- start Eleventy dev server ---"

  npx @gerhobbelt/eleventy --config=docs-src/.eleventy.js

  echo done.
  ;;

s )
  echo "--- start live-server dev server ---"

  mkdir -p docs
  live-server .

  echo done.
  ;;

c )
  echo "--- clean website output/dist directory ---"

  rm -rf docs

  echo done.
  ;;

f )
  echo "--- build font specimen sample pages ---"

  cd docs-src/_meta/fonts/
  npx sass font-specimen.scss font-specimen.css

  echo done.
  ;;

* )
  cat <<EOT
$0 [-b] [-d] [-s] [-c]

build & run vuepress-based documentation website QiQQa.ORG.

-b       : (re)build website from sources
-e       : (re)build website using eleventy
-d       : run the eleventy dev server
-f       : build the font specimen sample pages, etc.
-s       : run the live_server local dev webserver
-c       : clean the website output/dist directory

Note
----
  You MUST specify a commandline option to have this script execute
  *anything*. This is done to protect you from inadvertently executing
  this long-running script when all you wanted was see what it'd do.

EOT
  ;;
esac


popd                                                                                                    2> /dev/null  > /dev/null


