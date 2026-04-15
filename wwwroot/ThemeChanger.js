var themeChanger = {
    changeCss: function (cssFileUrl) {
        var oldLink = document.getElementById("TelerikThemeLink"); // we have this id on the <link> that references the theme

        if (cssFileUrl === oldLink.getAttribute("href")) {
            return;
        }

        var newLink = document.createElement("link");
        newLink.setAttribute("id", "TelerikThemeLink");
        newLink.setAttribute("rel", "stylesheet");
        newLink.setAttribute("type", "text/css");
        newLink.setAttribute("href", cssFileUrl);
        newLink.onload = () => {
            oldLink.parentElement.removeChild(oldLink);
        };

        document.getElementsByTagName("head")[0].appendChild(newLink);
    }
}