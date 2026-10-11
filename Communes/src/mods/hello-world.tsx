import React from "react";

export const HelloWorldComponent = () => {
    return React.createElement(
        "div",
        { style: { padding: "16px", color: "white", fontSize: "16px" } },
        React.createElement("h2", null, "Communes"),
        React.createElement("p", null, "Gestion des communes"),
        React.createElement(
            "p",
            { style: { opacity: 0.7, fontSize: "13px" } },
            "Sélectionnez une commune pour consulter ses statistiques."
        )
    );
};